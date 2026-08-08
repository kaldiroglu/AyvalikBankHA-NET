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
        await RequireCustomerExistsAsync(cmd.CallerId);
        var account = CheckingAccount.Open(cmd.CallerId, cmd.Currency, cmd.OverdraftLimit);
        return (CheckingAccount)await accountRepository.SaveAsync(account);
    }

    public async Task<SavingsAccount> OpenSavingsAsync(IOpenSavingsAccountUseCase.Command cmd)
    {
        await RequireCustomerExistsAsync(cmd.CallerId);
        var account = SavingsAccount.Open(cmd.CallerId, cmd.Currency, cmd.AnnualInterestRate);
        return (SavingsAccount)await accountRepository.SaveAsync(account);
    }

    public async Task<TimeDepositAccount> OpenTimeDepositAsync(IOpenTimeDepositAccountUseCase.Command cmd)
    {
        await RequireCustomerExistsAsync(cmd.CallerId);
        var account = TimeDepositAccount.Open(cmd.CallerId, cmd.Currency, cmd.Principal,
            cmd.MaturityDate, cmd.AnnualInterestRate);
        return (TimeDepositAccount)await accountRepository.SaveAsync(account);
    }

    // ── Operations ────────────────────────────────────────────────────────

    public async Task<Transaction> DepositAsync(IDepositMoneyUseCase.Command cmd)
    {
        var account = await Find(cmd.AccountId);
        RequireOwner(account, cmd.CallerId);
        Transaction tx;
        try { tx = account.Deposit(cmd.Amount); }
        catch (AccountRuleViolation e) { throw Translate(e); }
        await accountRepository.SaveAsync(account);
        return await transactionRepository.SaveAsync(tx);
    }

    public async Task<Transaction> WithdrawAsync(IWithdrawMoneyUseCase.Command cmd)
    {
        var account = await Find(cmd.AccountId);
        RequireOwner(account, cmd.CallerId);
        var owner = await FindCustomer(account.OwnerId);
        try { transferDomainService.RequireWithdrawalWithinLimit(cmd.Amount, owner.Tier); }
        catch (AccountRuleViolation e) { throw Translate(e); }

        Transaction tx;
        try { tx = account.Withdraw(cmd.Amount); }
        catch (AccountRuleViolation e) { throw Translate(e); }
        await accountRepository.SaveAsync(account);
        return await transactionRepository.SaveAsync(tx);
    }

    public async Task TransferAsync(ITransferMoneyUseCase.Command cmd)
    {
        var source = await Find(cmd.SourceAccountId);
        RequireOwner(source, cmd.CallerId);
        // The TARGET is deliberately NOT ownership-checked: sending money to another
        // customer is the entire point of a transfer.
        var target = await Find(cmd.TargetAccountId);
        var sourceOwner = await FindCustomer(source.OwnerId);

        try { transferDomainService.RequireTransferWithinLimit(cmd.Amount, sourceOwner.Tier); }
        catch (AccountRuleViolation e) { throw Translate(e); }

        var sameCustomer = source.OwnerId == target.OwnerId;
        var feePercent = await settingsRepository.GetTransferFeePercentAsync();
        var fee = transferDomainService.CalculateFee(cmd.Amount, sameCustomer, feePercent, sourceOwner.Tier);

        Transaction outTx, inTx;
        try
        {
            outTx = source.TransferOut(cmd.Amount, fee, target.Id);
            inTx = target.TransferIn(cmd.Amount, source.Id);
        }
        catch (AccountRuleViolation e) { throw Translate(e); }

        await accountRepository.SaveAsync(source);
        await accountRepository.SaveAsync(target);
        await transactionRepository.SaveAsync(outTx);
        await transactionRepository.SaveAsync(inTx);
    }

    public async Task<Money> GetBalanceAsync(Guid callerId, Guid accountId)
    {
        var account = await Find(accountId);
        RequireOwner(account, callerId);
        return account.Balance;
    }

    public async Task<List<Transaction>> GetTransactionsAsync(Guid callerId, Guid accountId)
    {
        RequireOwner(await Find(accountId), callerId);
        return await transactionRepository.FindByAccountIdAsync(accountId);
    }

    public async Task<List<Account>> ListAccountsAsync(Guid callerId, Guid ownerId)
    {
        RequireSelf(ownerId, callerId);
        await RequireCustomerExistsAsync(ownerId);
        return await accountRepository.FindByOwnerIdAsync(ownerId);
    }

    public async Task FreezeAccountAsync(Guid accountId)
    {
        var account = await Find(accountId);
        try { account.Freeze(); }
        catch (AccountRuleViolation e) { throw Translate(e); }
        await accountRepository.SaveAsync(account);
    }

    public async Task UnfreezeAccountAsync(Guid accountId)
    {
        var account = await Find(accountId);
        try { account.Unfreeze(); }
        catch (AccountRuleViolation e) { throw Translate(e); }
        await accountRepository.SaveAsync(account);
    }

    public async Task CloseAccountAsync(Guid accountId)
    {
        var account = await Find(accountId);
        try { account.Close(); }
        catch (AccountRuleViolation e) { throw Translate(e); }
        await accountRepository.SaveAsync(account);
    }

    public async Task<Transaction> AccrueInterestAsync(IAccrueInterestUseCase.Command cmd)
    {
        var account = await Find(cmd.AccountId);
        if (account is not SavingsAccount savings)
            throw new InvalidAccountOperationException("Account is not a savings account");
        Transaction tx;
        try { tx = savings.AccrueInterest(cmd.Year, cmd.Month); }
        catch (AccountRuleViolation e) { throw Translate(e); }
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
        catch (AccountRuleViolation e) { throw Translate(e); }
        await accountRepository.SaveAsync(td);
        return await transactionRepository.SaveAsync(tx);
    }

    public Task SetTransferFeeAsync(ISetTransferFeeUseCase.Command cmd)
    {
        // Defence in depth. The DTO's [Range] already rejects a negative percentage over REST, so
        // this guard is unreachable from the API - which is exactly why it needs its own test.
        // Matches AyvalikBankHA-JAVA; see its Refactorings.md entry 2.
        if (cmd.FeePercent < 0m)
            throw new ArgumentException("Transfer fee percent cannot be negative");
        return settingsRepository.SetTransferFeePercentAsync(cmd.FeePercent);
    }

    // Security: the caller must own the account. Mirrors the Java fix - see
    // AyvalikBankHA-JAVA Refactorings.md entry 3.
    private static void RequireOwner(Account account, Guid callerId)
    {
        if (account.OwnerId != callerId)
            throw new AyvalikBankHA.Api.Application.Exception.UnauthorizedAccessException("Account does not belong to the caller");
    }

    private static void RequireSelf(Guid subject, Guid callerId)
    {
        if (subject != callerId)
            throw new AyvalikBankHA.Api.Application.Exception.UnauthorizedAccessException("Callers may only act on their own customer record");
    }

    /// <summary>
    /// Maps a domain refusal to the application exception that carries its HTTP meaning.
    ///
    /// <para>Replaces a chain of <c>when (e.Message.Contains(...))</c> filters, where rewording a
    /// message silently changed the response status. C# cannot prove this switch exhaustive over a
    /// class hierarchy, so the discard arm throws — a missing case fails loudly rather than
    /// silently falling through. Mirrors AyvalikBankHA-JAVA Refactorings.md entry 4.</para>
    /// </summary>
    private static System.Exception Translate(AccountRuleViolation violation) => violation switch
    {
        AccountNotActiveException e         => new AccountNotOperableException(e.Message),
        InsufficientBalanceException e      => new InsufficientFundsException(e.Message),
        OperationNotPermittedException e    => new InvalidAccountOperationException(e.Message),
        TransactionLimitExceededException e => new LimitExceededException(e.Message),
        _ => throw new NotSupportedException($"Unhandled refusal type {violation.GetType().Name}"),
    };

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
