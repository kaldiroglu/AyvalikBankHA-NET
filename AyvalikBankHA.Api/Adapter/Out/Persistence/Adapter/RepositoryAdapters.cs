using AyvalikBankHA.Api.Adapter.Out.Persistence.Mapper;
using AyvalikBankHA.Api.Domain.Model;
using AyvalikBankHA.Api.Domain.Port.Out;
using Microsoft.EntityFrameworkCore;

namespace AyvalikBankHA.Api.Adapter.Out.Persistence.Adapter;

public class CustomerPersistenceAdapter(BankDbContext db) : ICustomerRepositoryPort
{
    public async Task<Customer> SaveAsync(Customer customer)
    {
        var jpa = CustomerMapper.ToJpa(customer);
        var existing = await db.Customers.FindAsync(jpa.Id);
        if (existing is null) db.Customers.Add(jpa);
        else { existing.Name = jpa.Name; existing.Email = jpa.Email; existing.Role = jpa.Role; existing.CurrentPassword = jpa.CurrentPassword; }
        await db.SaveChangesAsync();
        return CustomerMapper.ToDomain(jpa);
    }

    public async Task<Customer?> FindByIdAsync(Guid id)
    {
        var jpa = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return jpa is null ? null : CustomerMapper.ToDomain(jpa);
    }

    public async Task<Customer?> FindByEmailAsync(string email)
    {
        var jpa = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Email == email);
        return jpa is null ? null : CustomerMapper.ToDomain(jpa);
    }

    public async Task<List<Customer>> FindAllAsync() =>
        (await db.Customers.AsNoTracking().ToListAsync()).Select(CustomerMapper.ToDomain).ToList();

    public async Task DeleteByIdAsync(Guid id)
    {
        var jpa = await db.Customers.FindAsync(id);
        if (jpa is not null) { db.Customers.Remove(jpa); await db.SaveChangesAsync(); }
    }

    public Task<bool> ExistsByIdAsync(Guid id) => db.Customers.AnyAsync(c => c.Id == id);
}

public class AccountPersistenceAdapter(BankDbContext db) : IAccountRepositoryPort
{
    public async Task<Account> SaveAsync(Account account)
    {
        var jpa = AccountMapper.ToJpa(account);
        var existing = await db.Accounts.FindAsync(jpa.Id);
        if (existing is null) db.Accounts.Add(jpa);
        else { existing.OwnerId = jpa.OwnerId; existing.Currency = jpa.Currency; existing.Balance = jpa.Balance; existing.Status = jpa.Status; }
        await db.SaveChangesAsync();
        return AccountMapper.ToDomain(jpa);
    }

    public async Task<Account?> FindByIdAsync(Guid id)
    {
        var jpa = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        return jpa is null ? null : AccountMapper.ToDomain(jpa);
    }

    public async Task<List<Account>> FindByOwnerIdAsync(Guid ownerId) =>
        (await db.Accounts.AsNoTracking().Where(a => a.OwnerId == ownerId).ToListAsync())
            .Select(AccountMapper.ToDomain).ToList();

    public Task<bool> ExistsByIdAsync(Guid id) => db.Accounts.AnyAsync(a => a.Id == id);
}

public class TransactionPersistenceAdapter(BankDbContext db) : ITransactionRepositoryPort
{
    public async Task<Transaction> SaveAsync(Transaction tx)
    {
        var jpa = TransactionMapper.ToJpa(tx);
        db.Transactions.Add(jpa);
        await db.SaveChangesAsync();
        return TransactionMapper.ToDomain(jpa);
    }

    public async Task<List<Transaction>> FindByAccountIdAsync(Guid accountId) =>
        (await db.Transactions.AsNoTracking().Where(t => t.AccountId == accountId).ToListAsync())
            .Select(TransactionMapper.ToDomain).ToList();
}

public class SettingsPersistenceAdapter(BankDbContext db) : ISettingsRepositoryPort
{
    public async Task<decimal> GetTransferFeePercentAsync()
    {
        var s = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "TRANSFER_FEE_PERCENT");
        return s is null ? 0m : decimal.Parse(s.Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task SetTransferFeePercentAsync(decimal percent)
    {
        var s = await db.Settings.FindAsync("TRANSFER_FEE_PERCENT");
        var v = percent.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (s is null) db.Settings.Add(new Entity.SettingsJpaEntity { Key = "TRANSFER_FEE_PERCENT", Value = v });
        else s.Value = v;
        await db.SaveChangesAsync();
    }
}
