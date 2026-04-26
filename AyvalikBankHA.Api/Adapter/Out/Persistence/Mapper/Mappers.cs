using AyvalikBankHA.Api.Adapter.Out.Persistence.Entity;
using AyvalikBankHA.Api.Domain.Model;

namespace AyvalikBankHA.Api.Adapter.Out.Persistence.Mapper;

public static class CustomerMapper
{
    public static Customer ToDomain(CustomerJpaEntity e) =>
        new(e.Id, e.Name, e.Email, e.Role,
            string.IsNullOrEmpty(e.Tier) ? CustomerTier.STANDARD : Enum.Parse<CustomerTier>(e.Tier),
            e.CurrentPassword);

    public static CustomerJpaEntity ToJpa(Customer c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Email = c.Email,
        Role = c.Role,
        Tier = c.Tier.ToString(),
        CurrentPassword = c.CurrentPasswordHash
    };
}

public static class AccountMapper
{
    public static Account ToDomain(AccountJpaEntity e)
    {
        var currency = Enum.Parse<Currency>(e.Currency);
        var status = Enum.Parse<AccountStatus>(e.Status);
        var type = string.IsNullOrEmpty(e.Type) ? AccountType.CHECKING : Enum.Parse<AccountType>(e.Type);
        var balance = new Money(e.Balance, currency);
        return type switch
        {
            AccountType.CHECKING => new CheckingAccount(e.Id, e.OwnerId, currency, balance, status,
                new Money(e.OverdraftLimit ?? 0m, currency)),
            AccountType.SAVINGS => new SavingsAccount(e.Id, e.OwnerId, currency, balance, status,
                e.InterestRate ?? 0m, e.LastAccrualDate),
            AccountType.TIME_DEPOSIT => new TimeDepositAccount(e.Id, e.OwnerId, currency, balance, status,
                new Money(e.Principal ?? 0m, currency),
                e.OpenedOn ?? DateOnly.FromDateTime(DateTime.UtcNow),
                e.MaturityDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1),
                e.InterestRate ?? 0m,
                e.Matured ?? false),
            _ => throw new InvalidOperationException($"Unknown AccountType: {type}")
        };
    }

    public static AccountJpaEntity ToJpa(Account a) => a switch
    {
        CheckingAccount c => new AccountJpaEntity
        {
            Id = c.Id, OwnerId = c.OwnerId, Currency = c.Currency.ToString(),
            Balance = c.Balance.Amount, Status = c.Status.ToString(), Type = "CHECKING",
            OverdraftLimit = c.OverdraftLimit.Amount
        },
        SavingsAccount s => new AccountJpaEntity
        {
            Id = s.Id, OwnerId = s.OwnerId, Currency = s.Currency.ToString(),
            Balance = s.Balance.Amount, Status = s.Status.ToString(), Type = "SAVINGS",
            InterestRate = s.AnnualInterestRate, LastAccrualDate = s.LastAccrualDate
        },
        TimeDepositAccount t => new AccountJpaEntity
        {
            Id = t.Id, OwnerId = t.OwnerId, Currency = t.Currency.ToString(),
            Balance = t.Balance.Amount, Status = t.Status.ToString(), Type = "TIME_DEPOSIT",
            Principal = t.Principal.Amount, OpenedOn = t.OpenedOn, MaturityDate = t.MaturityDate,
            InterestRate = t.AnnualInterestRate, Matured = t.Matured
        },
        _ => throw new InvalidOperationException($"Unknown Account subtype: {a.GetType().Name}")
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
