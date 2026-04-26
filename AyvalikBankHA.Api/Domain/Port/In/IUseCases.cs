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
    record Command(Guid CustomerId, string RawNewPassword);
    Task ChangePasswordAsync(Command cmd);
}

// Account use cases
public interface ICreateAccountUseCase
{
    record Command(Guid OwnerId, Currency Currency);
    Task<Account> CreateAccountAsync(Command cmd);
}

public interface IDepositMoneyUseCase
{
    record Command(Guid AccountId, Money Amount);
    Task<Transaction> DepositAsync(Command cmd);
}

public interface IWithdrawMoneyUseCase
{
    record Command(Guid AccountId, Money Amount);
    Task<Transaction> WithdrawAsync(Command cmd);
}

public interface ITransferMoneyUseCase
{
    record Command(Guid SourceAccountId, Guid TargetAccountId, Money Amount);
    Task TransferAsync(Command cmd);
}

public interface IGetBalanceUseCase
{
    Task<Money> GetBalanceAsync(Guid accountId);
}

public interface IGetTransactionsUseCase
{
    Task<List<Transaction>> GetTransactionsAsync(Guid accountId);
}

public interface IListAccountsUseCase
{
    Task<List<Account>> ListAccountsAsync(Guid ownerId);
}

public interface IFreezeAccountUseCase  { Task FreezeAccountAsync(Guid accountId); }
public interface IUnfreezeAccountUseCase { Task UnfreezeAccountAsync(Guid accountId); }
public interface ICloseAccountUseCase    { Task CloseAccountAsync(Guid accountId); }

public interface ISetTransferFeeUseCase
{
    record Command(decimal FeePercent);
    Task SetTransferFeeAsync(Command cmd);
}
