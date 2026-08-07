using System.ComponentModel.DataAnnotations;
namespace AyvalikBankHA.Api.Adapter.Out.Persistence.Entity;

// JPA-equivalent entities (anemic DTOs). They never cross the persistence boundary.

public class CustomerJpaEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string Tier { get; set; } = "";
    public string CurrentPassword { get; set; } = "";
}

public class AccountJpaEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Optimistic-lock token. Incremented by the persistence layer on every write and included in
    /// the UPDATE's WHERE clause, so a write based on a stale read affects no rows and EF Core
    /// raises DbUpdateConcurrencyException.
    ///
    /// Without it two concurrent withdrawals both read the same balance, both write their own
    /// result, and one silently disappears while both transaction rows persist.
    /// Mirrors AyvalikBankHA-JAVA Refactorings.md entry 5.
    /// </summary>
    [ConcurrencyCheck]
    public long Version { get; set; }

    public Guid OwnerId { get; set; }
    public string Currency { get; set; } = "";
    public decimal Balance { get; set; }
    public string Status { get; set; } = "";
    public string Type { get; set; } = "";
    // Checking-specific
    public decimal? OverdraftLimit { get; set; }
    // Savings-specific
    public decimal? InterestRate { get; set; }
    public DateOnly? LastAccrualDate { get; set; }
    // Time-deposit-specific
    public decimal? Principal { get; set; }
    public DateOnly? OpenedOn { get; set; }
    public DateOnly? MaturityDate { get; set; }
    public bool? Matured { get; set; }
}

public class TransactionJpaEntity
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Type { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string Description { get; set; } = "";
}

public class SettingsJpaEntity
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
