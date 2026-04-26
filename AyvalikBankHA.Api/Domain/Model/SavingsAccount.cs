namespace AyvalikBankHA.Api.Domain.Model;

public sealed class SavingsAccount : Account
{
    private const int MonthsPerYear = 12;
    public decimal AnnualInterestRate { get; }
    public DateOnly? LastAccrualDate { get; private set; }

    public SavingsAccount(Guid id, Guid ownerId, Currency currency,
        Money balance, AccountStatus status,
        decimal annualInterestRate, DateOnly? lastAccrualDate)
        : base(id, ownerId, currency, balance, status)
    {
        if (annualInterestRate < 0) throw new ArgumentException("Annual interest rate must be non-negative");
        AnnualInterestRate = annualInterestRate;
        LastAccrualDate = lastAccrualDate;
    }

    public static SavingsAccount Open(Guid ownerId, Currency currency, decimal annualInterestRate) =>
        new(Guid.NewGuid(), ownerId, currency, Money.Zero(currency), AccountStatus.ACTIVE,
            annualInterestRate, null);

    public override AccountType Type => AccountType.SAVINGS;

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
        if (!Balance.IsGreaterThanOrEqualTo(amount))
            throw new InvalidOperationException("Insufficient funds");
        Balance = Balance.Subtract(amount);
        return Transaction.Create(Id, TransactionType.WITHDRAWAL, amount, "Withdrawal");
    }

    public override Transaction TransferOut(Money amount, Money fee, Guid targetAccountId)
    {
        RequireOperable();
        RequireSameCurrency(amount);
        var totalDebit = fee.Amount > 0 ? amount.Add(fee) : amount;
        if (!Balance.IsGreaterThanOrEqualTo(totalDebit))
            throw new InvalidOperationException("Insufficient funds for transfer including fee");
        Balance = Balance.Subtract(totalDebit);
        var desc = $"Transfer out to {targetAccountId}" + (fee.Amount > 0 ? $" (fee: {fee.Amount})" : "");
        return Transaction.Create(Id, TransactionType.TRANSFER_OUT, amount, desc);
    }

    public Transaction AccrueInterest(int year, int month)
    {
        // FROZEN accounts can still accrue: it's a system action, not a customer action.
        if (IsTerminal) throw new InvalidOperationException("Cannot accrue interest on a closed account");
        var firstOfNextMonth = new DateOnly(year, month, 1).AddMonths(1);
        if (LastAccrualDate is { } last && firstOfNextMonth <= last)
            throw new InvalidOperationException($"Interest already accrued for or after {year:D4}-{month:D2}");

        var monthlyRate = AnnualInterestRate / MonthsPerYear;
        var interestAmount = Math.Round(Balance.Amount * monthlyRate, 2, MidpointRounding.AwayFromZero);
        var interest = new Money(interestAmount, Currency);
        Balance = Balance.Add(interest);
        LastAccrualDate = firstOfNextMonth;
        return Transaction.Create(Id, TransactionType.INTEREST, interest, $"Interest accrual for {year:D4}-{month:D2}");
    }
}
