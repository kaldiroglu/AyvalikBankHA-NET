namespace AyvalikBankHA.Api.Domain.Model;

public class Transaction
{
    public Guid Id { get; }
    public Guid AccountId { get; }
    public TransactionType Type { get; }
    public Money Amount { get; }
    public DateTimeOffset Timestamp { get; }
    public string Description { get; }

    public Transaction(Guid id, Guid accountId, TransactionType type, Money amount, DateTimeOffset timestamp, string description)
    {
        Id = id;
        AccountId = accountId;
        Type = type;
        Amount = amount;
        Timestamp = timestamp;
        Description = description;
    }

    public static Transaction Create(Guid accountId, TransactionType type, Money amount, string description) =>
        new(Guid.NewGuid(), accountId, type, amount, DateTimeOffset.UtcNow, description);
}
