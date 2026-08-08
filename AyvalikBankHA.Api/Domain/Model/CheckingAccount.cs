namespace AyvalikBankHA.Api.Domain.Model;

public sealed class CheckingAccount : Account
{
    public Money OverdraftLimit { get; }

    public CheckingAccount(Guid id, Guid ownerId, Currency currency,
        Money balance, AccountStatus status, Money overdraftLimit)
        : base(id, ownerId, currency, balance, status)
    {
        if (overdraftLimit.Currency != currency)
            throw new ArgumentException("Overdraft limit currency must match account currency");
        if (overdraftLimit.Amount < 0)
            throw new ArgumentException("Overdraft limit cannot be negative");
        OverdraftLimit = overdraftLimit;
    }

    public static CheckingAccount Open(Guid ownerId, Currency currency, Money? overdraftLimit = null) =>
        new(Guid.NewGuid(), ownerId, currency, Money.Zero(currency), AccountStatus.ACTIVE,
            overdraftLimit ?? Money.Zero(currency));

    public override AccountType Type => AccountType.CHECKING;

    public override Transaction Deposit(TransactionAmount amount)
    {
        RequireOperable();
        RequireSameCurrency(amount);
        Balance = Balance.Add(amount.Value);
        return Transaction.Create(Id, TransactionType.DEPOSIT, amount.Value, "Deposit");
    }

    public override Transaction Withdraw(TransactionAmount amount)
    {
        RequireOperable();
        RequireSameCurrency(amount);
        var projected = Balance.Amount - amount.Value.Amount;
        var floor = -OverdraftLimit.Amount;
        if (projected < floor)
        {
            if (OverdraftLimit.Amount == 0)
                throw new InsufficientBalanceException("Insufficient funds");
            throw new InsufficientBalanceException("Withdrawal exceeds overdraft limit");
        }
        Balance = new Money(projected, Currency);
        return Transaction.Create(Id, TransactionType.WITHDRAWAL, amount.Value, "Withdrawal");
    }

    public override Transaction TransferOut(TransactionAmount amount, Money fee, Guid targetAccountId)
    {
        RequireOperable();
        RequireSameCurrency(amount);
        var totalDebit = fee.Amount > 0 ? amount.Value.Add(fee) : amount.Value;
        var projected = Balance.Amount - totalDebit.Amount;
        var floor = -OverdraftLimit.Amount;
        if (projected < floor)
            throw new InsufficientBalanceException("Insufficient funds for transfer including fee");
        Balance = new Money(projected, Currency);
        var desc = $"Transfer out to {targetAccountId}" + (fee.Amount > 0 ? $" (fee: {fee.Amount})" : "");
        return Transaction.Create(Id, TransactionType.TRANSFER_OUT, amount.Value, desc);
    }
}
