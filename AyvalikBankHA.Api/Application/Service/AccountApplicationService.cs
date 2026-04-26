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
    ICreateAccountUseCase,
    IDepositMoneyUseCase,
    IWithdrawMoneyUseCase,
    ITransferMoneyUseCase,
    IGetBalanceUseCase,
    IGetTransactionsUseCase,
    IListAccountsUseCase,
    IFreezeAccountUseCase,
    IUnfreezeAccountUseCase,
    ICloseAccountUseCase,
    ISetTransferFeeUseCase
{
    public async Task<Account> CreateAccountAsync(ICreateAccountUseCase.Command cmd)
    {
        if (!await customerRepository.ExistsByIdAsync(cmd.OwnerId))
            throw new CustomerNotFoundException($"Customer not found: {cmd.OwnerId}");
        return await accountRepository.SaveAsync(Account.Open(cmd.OwnerId, cmd.Currency));
    }

    public async Task<Transaction> DepositAsync(IDepositMoneyUseCase.Command cmd)
    {
        var account = await Find(cmd.AccountId);
        Transaction tx;
        try { tx = account.Deposit(cmd.Amount); }
        catch (InvalidOperationException e) { throw new AccountNotOperableException(e.Message); }
        await accountRepository.SaveAsync(account);
        return await transactionRepository.SaveAsync(tx);
    }

    public async Task<Transaction> WithdrawAsync(IWithdrawMoneyUseCase.Command cmd)
    {
        var account = await Find(cmd.AccountId);
        Transaction tx;
        try { tx = account.Withdraw(cmd.Amount); }
        catch (InvalidOperationException e) when (e.Message.Contains("frozen") || e.Message.Contains("closed"))
        { throw new AccountNotOperableException(e.Message); }
        catch (InvalidOperationException e) { throw new InsufficientFundsException(e.Message); }
        await accountRepository.SaveAsync(account);
        return await transactionRepository.SaveAsync(tx);
    }

    public async Task TransferAsync(ITransferMoneyUseCase.Command cmd)
    {
        var source = await Find(cmd.SourceAccountId);
        var target = await Find(cmd.TargetAccountId);
        var sameCustomer = source.OwnerId == target.OwnerId;
        var feePercent = await settingsRepository.GetTransferFeePercentAsync();
        var fee = transferDomainService.CalculateFee(cmd.Amount, sameCustomer, feePercent);

        Transaction outTx, inTx;
        try
        {
            outTx = source.TransferOut(cmd.Amount, fee, target.Id);
            inTx = target.TransferIn(cmd.Amount, source.Id);
        }
        catch (InvalidOperationException e) when (e.Message.Contains("frozen") || e.Message.Contains("closed"))
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
        if (!await customerRepository.ExistsByIdAsync(ownerId))
            throw new CustomerNotFoundException($"Customer not found: {ownerId}");
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

    public Task SetTransferFeeAsync(ISetTransferFeeUseCase.Command cmd) =>
        settingsRepository.SetTransferFeePercentAsync(cmd.FeePercent);

    private async Task<Account> Find(Guid id) =>
        await accountRepository.FindByIdAsync(id) ?? throw new AccountNotFoundException($"Account not found: {id}");
}
