using AyvalikBankHA.Api.Domain.Model;

namespace AyvalikBankHA.Api.Domain.Port.Out;

public interface ICustomerRepositoryPort
{
    Task<Customer> SaveAsync(Customer customer);
    Task<Customer?> FindByIdAsync(Guid id);
    Task<Customer?> FindByEmailAsync(string email);
    Task<List<Customer>> FindAllAsync();
    Task DeleteByIdAsync(Guid id);
    Task<bool> ExistsByIdAsync(Guid id);
}

public interface IAccountRepositoryPort
{
    Task<Account> SaveAsync(Account account);
    Task<Account?> FindByIdAsync(Guid id);
    Task<List<Account>> FindByOwnerIdAsync(Guid ownerId);
    Task<bool> ExistsByIdAsync(Guid id);
}

public interface ITransactionRepositoryPort
{
    Task<Transaction> SaveAsync(Transaction tx);
    Task<List<Transaction>> FindByAccountIdAsync(Guid accountId);
}

public interface ISettingsRepositoryPort
{
    Task<decimal> GetTransferFeePercentAsync();
    Task SetTransferFeePercentAsync(decimal percent);
}

public interface IPasswordHasherPort
{
    string Hash(string raw);
    bool Matches(string raw, string hash);
}
