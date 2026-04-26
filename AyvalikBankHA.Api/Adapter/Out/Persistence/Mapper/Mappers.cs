using AyvalikBankHA.Api.Adapter.Out.Persistence.Entity;
using AyvalikBankHA.Api.Domain.Model;

namespace AyvalikBankHA.Api.Adapter.Out.Persistence.Mapper;

public static class CustomerMapper
{
    public static Customer ToDomain(CustomerJpaEntity e) =>
        new(e.Id, e.Name, e.Email, e.Role, e.CurrentPassword);

    public static CustomerJpaEntity ToJpa(Customer c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Email = c.Email,
        Role = c.Role,
        CurrentPassword = c.CurrentPasswordHash
    };
}

public static class AccountMapper
{
    public static Account ToDomain(AccountJpaEntity e)
    {
        var currency = Enum.Parse<Currency>(e.Currency);
        return new Account(e.Id, e.OwnerId, currency, new Money(e.Balance, currency),
            Enum.Parse<AccountStatus>(e.Status));
    }

    public static AccountJpaEntity ToJpa(Account a) => new()
    {
        Id = a.Id,
        OwnerId = a.OwnerId,
        Currency = a.Currency.ToString(),
        Balance = a.Balance.Amount,
        Status = a.Status.ToString()
    };
}

public static class TransactionMapper
{
    public static Transaction ToDomain(TransactionJpaEntity e)
    {
        var currency = Enum.Parse<Currency>(e.Currency);
        return new Transaction(e.Id, e.AccountId, Enum.Parse<TransactionType>(e.Type),
            new Money(e.Amount, currency), e.CreatedAt, e.Description);
    }

    public static TransactionJpaEntity ToJpa(Transaction t) => new()
    {
        Id = t.Id,
        AccountId = t.AccountId,
        Type = t.Type.ToString(),
        Amount = t.Amount.Amount,
        Currency = t.Amount.Currency.ToString(),
        CreatedAt = t.Timestamp,
        Description = t.Description
    };
}
