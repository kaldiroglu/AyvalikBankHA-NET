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

    public override Transaction Deposit(Money amount)
    {
        RequireOperable();
        RequireSameCurrency(amount);
        if (amount.Amount <= 0) throw new ArgumentException("Deposit amount must be positive");
        Balance = Balance.Add(amount);
        return Transaction.Create(Id, TransactionType.DEPOSIT, amount, "Deposit");
    }

    public override Transaction Withdraw(Money amount)
    {
        RequireOperable();
        RequireSameCurrency(amount);
        if (amount.Amount <= 0) throw new ArgumentException("Withdrawal amount must be positive");
        var projected = Balance.Amount - amount.Amount;
        var floor = -OverdraftLimit.Amount;
        if (projected < floor)
        {
            if (OverdraftLimit.Amount == 0)
                throw new InsufficientBalanceException("Insufficient funds");
            throw new InsufficientBalanceException("Withdrawal exceeds overdraft limit");
        }
        Balance = new Money(projected, Currency);
        return Transaction.Create(Id, TransactionType.WITHDRAWAL, amount, "Withdrawal");
    }

    public override Transaction TransferOut(Money amount, Money fee, Guid targetAccountId)
    {
        RequireOperable();
        RequireSameCurrency(amount);
        var totalDebit = fee.Amount > 0 ? amount.Add(fee) : amount;
        var projected = Balance.Amount - totalDebit.Amount;
        var floor = -OverdraftLimit.Amount;
        if (projected < floor)
            throw new InsufficientBalanceException("Insufficient funds for transfer including fee");
        Balance = new Money(projected, Currency);
        var desc = $"Transfer out to {targetAccountId}" + (fee.Amount > 0 ? $" (fee: {fee.Amount})" : "");
        return Transaction.Create(Id, TransactionType.TRANSFER_OUT, amount, desc);
    }
}
