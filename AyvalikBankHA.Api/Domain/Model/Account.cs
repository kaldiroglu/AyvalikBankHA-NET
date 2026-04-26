namespace AyvalikBankHA.Api.Domain.Model;

// Rich domain entity. Owns its own invariants — no setters that bypass the rules.
public class Account
{
    public Guid Id { get; }
    public Guid OwnerId { get; }
    public Currency Currency { get; }
    public Money Balance { get; private set; }
    public AccountStatus Status { get; private set; }

    public Account(Guid id, Guid ownerId, Currency currency, Money balance, AccountStatus status)
    {
        if (balance.Currency != currency)
            throw new ArgumentException("Balance currency must match account currency");
        Id = id;
        OwnerId = ownerId;
        Currency = currency;
        Balance = balance;
        Status = status;
    }

    public static Account Open(Guid ownerId, Currency currency) =>
        new(Guid.NewGuid(), ownerId, currency, Money.Zero(currency), AccountStatus.ACTIVE);

    // ── status transitions ────────────────────────────────────────────────

    public void Freeze()
    {
        if (Status == AccountStatus.CLOSED) throw new InvalidOperationException("Cannot freeze a closed account");
        if (Status == AccountStatus.FROZEN) throw new InvalidOperationException("Account is already frozen");
        Status = AccountStatus.FROZEN;
    }

    public void Unfreeze()
    {
        if (Status == AccountStatus.CLOSED) throw new InvalidOperationException("Cannot unfreeze a closed account");
        if (Status == AccountStatus.ACTIVE) throw new InvalidOperationException("Account is not frozen");
        Status = AccountStatus.ACTIVE;
    }

    public void Close()
    {
        if (Status == AccountStatus.CLOSED) throw new InvalidOperationException("Account is already closed");
        Status = AccountStatus.CLOSED;
    }

    // ── operations ────────────────────────────────────────────────────────

    public Transaction Deposit(Money amount)
    {
        RequireActive();
        RequireSameCurrency(amount);
        if (amount.Amount <= 0) throw new ArgumentException("Deposit amount must be positive");
        Balance = Balance.Add(amount);
        return Transaction.Create(Id, TransactionType.DEPOSIT, amount, "Deposit");
    }

    public Transaction Withdraw(Money amount)
    {
        RequireActive();
        RequireSameCurrency(amount);
        if (amount.Amount <= 0) throw new ArgumentException("Withdrawal amount must be positive");
        if (!Balance.IsGreaterThanOrEqualTo(amount)) throw new InvalidOperationException("Insufficient funds");
        Balance = Balance.Subtract(amount);
        return Transaction.Create(Id, TransactionType.WITHDRAWAL, amount, "Withdrawal");
    }

    public Transaction TransferOut(Money amount, Money fee, Guid targetAccountId)
    {
        RequireActive();
        RequireSameCurrency(amount);
        var totalDebit = fee.Amount > 0 ? amount.Add(fee) : amount;
        if (!Balance.IsGreaterThanOrEqualTo(totalDebit)) throw new InvalidOperationException("Insufficient funds for transfer including fee");
        Balance = Balance.Subtract(totalDebit);
        var desc = $"Transfer out to {targetAccountId}" + (fee.Amount > 0 ? $" (fee: {fee.Amount})" : "");
        return Transaction.Create(Id, TransactionType.TRANSFER_OUT, amount, desc);
    }

    public Transaction TransferIn(Money amount, Guid sourceAccountId)
    {
        RequireActive();
        RequireSameCurrency(amount);
        Balance = Balance.Add(amount);
        return Transaction.Create(Id, TransactionType.TRANSFER_IN, amount, $"Transfer in from {sourceAccountId}");
    }

    private void RequireActive()
    {
        if (Status == AccountStatus.FROZEN) throw new InvalidOperationException("Account is frozen");
        if (Status == AccountStatus.CLOSED) throw new InvalidOperationException("Account is closed");
    }

    private void RequireSameCurrency(Money amount)
    {
        if (amount.Currency != Currency)
            throw new ArgumentException($"Currency {amount.Currency} does not match account currency {Currency}");
    }
}
