namespace AyvalikBankHA.Api.Domain.Model;

// Rich domain entity. Owns its own invariants via the State pattern + per-type
// behavior in CheckingAccount / SavingsAccount / TimeDepositAccount subclasses.
public abstract class Account
{
    public Guid Id { get; }
    public Guid OwnerId { get; }
    public Currency Currency { get; }
    public Money Balance { get; protected set; }
    protected AccountState State { get; set; }

    public AccountStatus Status => State.Status;
    public abstract AccountType Type { get; }

    protected Account(Guid id, Guid ownerId, Currency currency, Money balance, AccountStatus status)
    {
        if (balance.Currency != currency)
            throw new ArgumentException("Balance currency must match account currency");
        Id = id;
        OwnerId = ownerId;
        Currency = currency;
        Balance = balance;
        State = AccountState.Of(status);
    }

    // ── Status transitions (delegated to State) ──────────────────────────

    public void Freeze() => State = State.Freeze();
    public void Unfreeze() => State = State.Unfreeze();
    public void Close() => State = State.Close();
    public bool IsTerminal => State.IsTerminal;
    protected void RequireOperable() => State.RequireOperable();

    // ── Operations: each subtype overrides ───────────────────────────────

    public abstract Transaction Deposit(TransactionAmount amount);
    public abstract Transaction Withdraw(TransactionAmount amount);
    public abstract Transaction TransferOut(TransactionAmount amount, Money fee, Guid targetAccountId);

    public Transaction TransferIn(TransactionAmount amount, Guid sourceAccountId)
    {
        State.RequireOperable();
        RequireSameCurrency(amount);
        Balance = Balance.Add(amount.Value);
        return Transaction.Create(Id, TransactionType.TRANSFER_IN, amount.Value, $"Transfer in from {sourceAccountId}");
    }

    // ── Guards ────────────────────────────────────────────────────────────

    protected void RequireSameCurrency(TransactionAmount amount)
    {
        if (amount.Currency != Currency)
            throw new ArgumentException($"Currency {amount.Currency} does not match account currency {Currency}");
    }
}
