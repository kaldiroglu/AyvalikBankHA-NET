using AyvalikBankHA.Api.Domain.Model;

namespace AyvalikBankHA.Api.Domain.Port.In;

// Customer use cases
public interface ICreateCustomerUseCase
{
    record Command(string Name, string Email, string RawPassword);
    Task<Customer> CreateCustomerAsync(Command cmd);
}

public interface IDeleteCustomerUseCase
{
    Task DeleteCustomerAsync(Guid customerId);
}

public interface IListCustomersUseCase
{
    Task<List<Customer>> ListCustomersAsync();
}

public interface IChangePasswordUseCase
{
    record Command(Guid CallerId, Guid CustomerId, string RawNewPassword);
    Task ChangePasswordAsync(Command cmd);
}

public interface IChangeCustomerTierUseCase
{
    record Command(Guid CustomerId, CustomerTier Tier);
    Task ChangeCustomerTierAsync(Command cmd);
}

// Account use cases — typed open methods (one per type)
public interface IOpenCheckingAccountUseCase
{
    record Command(Guid CallerId, Currency Currency, Money OverdraftLimit);
    Task<CheckingAccount> OpenCheckingAsync(Command cmd);
}

public interface IOpenSavingsAccountUseCase
{
    record Command(Guid CallerId, Currency Currency, decimal AnnualInterestRate);
    Task<SavingsAccount> OpenSavingsAsync(Command cmd);
}

public interface IOpenTimeDepositAccountUseCase
{
    record Command(Guid CallerId, Currency Currency, Money Principal,
                   DateOnly MaturityDate, decimal AnnualInterestRate);
    Task<TimeDepositAccount> OpenTimeDepositAsync(Command cmd);
}

public interface IDepositMoneyUseCase
{
    record Command(Guid CallerId, Guid AccountId, Money Amount);
    Task<Transaction> DepositAsync(Command cmd);
}

public interface IWithdrawMoneyUseCase
{
    record Command(Guid CallerId, Guid AccountId, Money Amount);
    Task<Transaction> WithdrawAsync(Command cmd);
}

public interface ITransferMoneyUseCase
{
    record Command(Guid CallerId, Guid SourceAccountId, Guid TargetAccountId, Money Amount);
    Task TransferAsync(Command cmd);
}

public interface IGetBalanceUseCase
{
    Task<Money> GetBalanceAsync(Guid callerId, Guid accountId);
}

public interface IGetTransactionsUseCase
{
    Task<List<Transaction>> GetTransactionsAsync(Guid callerId, Guid accountId);
}

public interface IListAccountsUseCase
{
    Task<List<Account>> ListAccountsAsync(Guid callerId, Guid ownerId);
}

public interface IFreezeAccountUseCase  { Task FreezeAccountAsync(Guid accountId); }
public interface IUnfreezeAccountUseCase { Task UnfreezeAccountAsync(Guid accountId); }
public interface ICloseAccountUseCase    { Task CloseAccountAsync(Guid accountId); }

public interface IAccrueInterestUseCase
{
    record Command(Guid AccountId, int Year, int Month);
    Task<Transaction> AccrueInterestAsync(Command cmd);
}

public interface IMatureTimeDepositUseCase
{
    record Command(Guid AccountId);
    Task<Transaction> MatureAsync(Command cmd);
}

public interface ISetTransferFeeUseCase
{
    record Command(decimal FeePercent);
    Task SetTransferFeeAsync(Command cmd);
}
