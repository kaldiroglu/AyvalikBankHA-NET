using AyvalikBankHA.Api.Domain.Model;

namespace AyvalikBankHA.Api.Domain.Port.In;

/// <summary>
/// Everything a <b>customer</b> can do with their own accounts — one conversation with one actor.
///
/// <para>A port in Cockburn's sense: it groups the operations belonging to a single kind of outside
/// party, rather than devoting an interface to each individual method. Twenty single-method
/// interfaces gave AccountController nine constructor parameters and AdminController ten, without
/// segregating anything — a customer-facing controller uses all nine customer-facing methods, so
/// splitting them bought no Interface Segregation at all.</para>
///
/// <para>Where ISP genuinely bites is the actor boundary: AdminController must not depend on
/// Deposit and Withdraw. That is the split these ports make and the old ones blurred.
/// Mirrors AyvalikBankHA-JAVA Refactorings.md entry 2.</para>
/// </summary>
public interface ICustomerAccountPort
{
    record OpenCheckingCommand(Guid CallerId, Currency Currency, Money OverdraftLimit);
    record OpenSavingsCommand(Guid CallerId, Currency Currency, decimal AnnualInterestRate);
    record OpenTimeDepositCommand(Guid CallerId, Currency Currency, Money Principal,
                                  DateOnly MaturityDate, decimal AnnualInterestRate);
    record DepositCommand(Guid CallerId, Guid AccountId, TransactionAmount Amount);
    record WithdrawCommand(Guid CallerId, Guid AccountId, TransactionAmount Amount);
    record TransferCommand(Guid CallerId, Guid SourceAccountId, Guid TargetAccountId, TransactionAmount Amount);

    Task<CheckingAccount> OpenCheckingAsync(OpenCheckingCommand cmd);
    Task<SavingsAccount> OpenSavingsAsync(OpenSavingsCommand cmd);
    Task<TimeDepositAccount> OpenTimeDepositAsync(OpenTimeDepositCommand cmd);
    Task<Transaction> DepositAsync(DepositCommand cmd);
    Task<Transaction> WithdrawAsync(WithdrawCommand cmd);
    Task TransferAsync(TransferCommand cmd);
    Task<Money> GetBalanceAsync(Guid callerId, Guid accountId);
    Task<List<Account>> ListAccountsAsync(Guid callerId, Guid ownerId);
    Task<List<Transaction>> GetTransactionsAsync(Guid callerId, Guid accountId);
}

/// <summary>Everything an <b>administrator</b> can do to an account they do not own.</summary>
public interface IAccountAdministrationPort
{
    record AccrueInterestCommand(Guid AccountId, int Year, int Month);
    record MatureCommand(Guid AccountId);

    Task FreezeAccountAsync(Guid accountId);
    Task UnfreezeAccountAsync(Guid accountId);
    Task CloseAccountAsync(Guid accountId);
    Task<Transaction> AccrueInterestAsync(AccrueInterestCommand cmd);
    Task<Transaction> MatureAsync(MatureCommand cmd);
}

/// <summary>Bank-wide configuration an <b>administrator</b> can change.</summary>
public interface IBankSettingsPort
{
    record SetTransferFeeCommand(decimal FeePercent);
    Task SetTransferFeeAsync(SetTransferFeeCommand cmd);
}

/// <summary>Everything an <b>administrator</b> can do to the customer roster.</summary>
public interface ICustomerAdministrationPort
{
    record CreateCustomerCommand(string Name, string Email, string RawPassword);
    record ChangeCustomerTierCommand(Guid CustomerId, CustomerTier Tier);

    Task<Customer> CreateCustomerAsync(CreateCustomerCommand cmd);
    Task DeleteCustomerAsync(Guid customerId);
    Task<List<Customer>> ListCustomersAsync();
    Task ChangeCustomerTierAsync(ChangeCustomerTierCommand cmd);
}

/// <summary>What a <b>customer</b> can do to their own record.</summary>
public interface ICustomerSelfServicePort
{
    record ChangePasswordCommand(Guid CallerId, Guid CustomerId, string RawNewPassword);
    Task ChangePasswordAsync(ChangePasswordCommand cmd);
}
