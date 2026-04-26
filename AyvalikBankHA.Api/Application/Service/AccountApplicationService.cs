using AyvalikBankHA.Api.Application.Exception;
using AyvalikBankHA.Api.Domain.Model;
using AyvalikBankHA.Api.Domain.Port.In;
using AyvalikBankHA.Api.Domain.Port.Out;
using AyvalikBankHA.Api.Domain.Service;

namespace AyvalikBankHA.Api.Application.Service;

public class AccountApplicationService(
    IAccountRepositoryPort accountRepository,
    ICustomerRepositoryPort customerRepository,
    ITransactionRepositoryPort transactionRepository,
    ISettingsRepositoryPort settingsRepository,
    TransferDomainService transferDomainService) :
    IOpenCheckingAccountUseCase,
    IOpenSavingsAccountUseCase,
    IOpenTimeDepositAccountUseCase,
    IDepositMoneyUseCase,
    IWithdrawMoneyUseCase,
    ITransferMoneyUseCase,
    IGetBalanceUseCase,
    IGetTransactionsUseCase,
    IListAccountsUseCase,
    IFreezeAccountUseCase,
    IUnfreezeAccountUseCase,
    ICloseAccountUseCase,
    IAccrueInterestUseCase,
    IMatureTimeDepositUseCase,
    ISetTransferFeeUseCase
{
    // ── Account opening ───────────────────────────────────────────────────

    public async Task<CheckingAccount> OpenCheckingAsync(IOpenCheckingAccountUseCase.Command cmd)
    {
        await RequireCustomerExistsAsync(cmd.OwnerId);
        var account = CheckingAccount.Open(cmd.OwnerId, cmd.Currency, cmd.OverdraftLimit);
        return (CheckingAccount)await accountRepository.SaveAsync(account);
    }

    public async Task<SavingsAccount> OpenSavingsAsync(IOpenSavingsAccountUseCase.Command cmd)
    {
        await RequireCustomerExistsAsync(cmd.OwnerId);
        var account = SavingsAccount.Open(cmd.OwnerId, cmd.Currency, cmd.AnnualInterestRate);
        return (SavingsAccount)await accountRepository.SaveAsync(account);
    }

    public async Task<TimeDepositAccount> OpenTimeDepositAsync(IOpenTimeDepositAccountUseCase.Command cmd)
    {
        await RequireCustomerExistsAsync(cmd.OwnerId);
        var account = TimeDepositAccount.Open(cmd.OwnerId, cmd.Currency, cmd.Principal,
            cmd.MaturityDate, cmd.AnnualInterestRate);
        return (TimeDepositAccount)await accountRepository.SaveAsync(account);
    }

    // ── Operations ────────────────────────────────────────────────────────

    public async Task<Transaction> DepositAsync(IDepositMoneyUseCase.Command cmd)
    {
        var account = await Find(cmd.AccountId);
        Transaction tx;
        try { tx = account.Deposit(cmd.Amount); }
        catch (InvalidOperationException e) when (e.Message.Contains("locked"))
        { throw new InvalidAccountOperationException(e.Message); }
        catch (InvalidOperationException e) { throw new AccountNotOperableException(e.Message); }
        await accountRepository.SaveAsync(account);
        return await transactionRepository.SaveAsync(tx);
    }

    public async Task<Transaction> WithdrawAsync(IWithdrawMoneyUseCase.Command cmd)
    {
        var account = await Find(cmd.AccountId);
        var owner = await FindCustomer(account.OwnerId);
        try { transferDomainService.RequireWithdrawalWithinLimit(cmd.Amount, owner.Tier); }
        catch (InvalidOperationException e) { throw new LimitExceededException(e.Message); }

        Transaction tx;
        try { tx = account.Withdraw(cmd.Amount); }
        catch (InvalidOperationException e) when (e.Message.Contains("frozen") || e.Message.Contains("closed") || e.Message.Contains("matured"))
        { throw new AccountNotOperableException(e.Message); }
        catch (InvalidOperationException e) { throw new InsufficientFundsException(e.Message); }
        await accountRepository.SaveAsync(account);
        return await transactionRepository.SaveAsync(tx);
    }

    public async Task TransferAsync(ITransferMoneyUseCase.Command cmd)
    {
        var source = await Find(cmd.SourceAccountId);
        var target = await Find(cmd.TargetAccountId);
        var sourceOwner = await FindCustomer(source.OwnerId);

        try { transferDomainService.RequireTransferWithinLimit(cmd.Amount, sourceOwner.Tier); }
        catch (InvalidOperationException e) { throw new LimitExceededException(e.Message); }

        var sameCustomer = source.OwnerId == target.OwnerId;
        var feePercent = await settingsRepository.GetTransferFeePercentAsync();
        var fee = transferDomainService.CalculateFee(cmd.Amount, sameCustomer, feePercent, sourceOwner.Tier);

        Transaction outTx, inTx;
        try
        {
            outTx = source.TransferOut(cmd.Amount, fee, target.Id);
            inTx = target.TransferIn(cmd.Amount, source.Id);
        }
        catch (InvalidOperationException e) when (e.Message.Contains("frozen") || e.Message.Contains("closed") || e.Message.Contains("transfers"))
        { throw new AccountNotOperableException(e.Message); }
        catch (InvalidOperationException e)
        { throw new InsufficientFundsException(e.Message); }

        await accountRepository.SaveAsync(source);
        await accountRepository.SaveAsync(target);
        await transactionRepository.SaveAsync(outTx);
        await transactionRepository.SaveAsync(inTx);
    }

    public async Task<Money> GetBalanceAsync(Guid accountId) => (await Find(accountId)).Balance;

    public async Task<List<Transaction>> GetTransactionsAsync(Guid accountId)
    {
        await Find(accountId);
        return await transactionRepository.FindByAccountIdAsync(accountId);
    }

    public async Task<List<Account>> ListAccountsAsync(Guid ownerId)
    {
        await RequireCustomerExistsAsync(ownerId);
        return await accountRepository.FindByOwnerIdAsync(ownerId);
    }

    public async Task FreezeAccountAsync(Guid accountId)
    {
        var account = await Find(accountId);
        try { account.Freeze(); }
        catch (InvalidOperationException e) { throw new AccountNotOperableException(e.Message); }
        await accountRepository.SaveAsync(account);
    }

    public async Task UnfreezeAccountAsync(Guid accountId)
    {
        var account = await Find(accountId);
        try { account.Unfreeze(); }
        catch (InvalidOperationException e) { throw new AccountNotOperableException(e.Message); }
        await accountRepository.SaveAsync(account);
    }

    public async Task CloseAccountAsync(Guid accountId)
    {
        var account = await Find(accountId);
        try { account.Close(); }
        catch (InvalidOperationException e) { throw new AccountNotOperableException(e.Message); }
        await accountRepository.SaveAsync(account);
    }

    public async Task<Transaction> AccrueInterestAsync(IAccrueInterestUseCase.Command cmd)
    {
        var account = await Find(cmd.AccountId);
        if (account is not SavingsAccount savings)
            throw new InvalidAccountOperationException("Account is not a savings account");
        Transaction tx;
        try { tx = savings.AccrueInterest(cmd.Year, cmd.Month); }
        catch (InvalidOperationException e) { throw new InvalidAccountOperationException(e.Message); }
        await accountRepository.SaveAsync(savings);
        return await transactionRepository.SaveAsync(tx);
    }

    public async Task<Transaction> MatureAsync(IMatureTimeDepositUseCase.Command cmd)
    {
        var account = await Find(cmd.AccountId);
        if (account is not TimeDepositAccount td)
            throw new InvalidAccountOperationException("Account is not a time deposit");
        Transaction tx;
        try { tx = td.Mature(DateOnly.FromDateTime(DateTime.UtcNow)); }
        catch (InvalidOperationException e) { throw new InvalidAccountOperationException(e.Message); }
        await accountRepository.SaveAsync(td);
        return await transactionRepository.SaveAsync(tx);
    }

    public Task SetTransferFeeAsync(ISetTransferFeeUseCase.Command cmd) =>
        settingsRepository.SetTransferFeePercentAsync(cmd.FeePercent);

    private async Task RequireCustomerExistsAsync(Guid id)
    {
        if (!await customerRepository.ExistsByIdAsync(id))
            throw new CustomerNotFoundException($"Customer not found: {id}");
    }

    private async Task<Account> Find(Guid id) =>
        await accountRepository.FindByIdAsync(id) ?? throw new AccountNotFoundException($"Account not found: {id}");

    private async Task<Customer> FindCustomer(Guid id) =>
        await customerRepository.FindByIdAsync(id) ?? throw new CustomerNotFoundException($"Customer not found: {id}");
}
