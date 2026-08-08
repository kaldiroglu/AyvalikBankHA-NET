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
        if (!Balance.IsGreaterThanOrEqualTo(amount.Value))
            throw new InsufficientBalanceException("Insufficient funds");
        Balance = Balance.Subtract(amount.Value);
        return Transaction.Create(Id, TransactionType.WITHDRAWAL, amount.Value, "Withdrawal");
    }

    public override Transaction TransferOut(TransactionAmount amount, Money fee, Guid targetAccountId)
    {
        RequireOperable();
        RequireSameCurrency(amount);
        var totalDebit = fee.Amount > 0 ? amount.Value.Add(fee) : amount.Value;
        if (!Balance.IsGreaterThanOrEqualTo(totalDebit))
            throw new InsufficientBalanceException("Insufficient funds for transfer including fee");
        Balance = Balance.Subtract(totalDebit);
        var desc = $"Transfer out to {targetAccountId}" + (fee.Amount > 0 ? $" (fee: {fee.Amount})" : "");
        return Transaction.Create(Id, TransactionType.TRANSFER_OUT, amount.Value, desc);
    }

    public Transaction AccrueInterest(int year, int month)
    {
        // FROZEN accounts can still accrue: it's a system action, not a customer action.
        if (IsTerminal) throw new AccountNotActiveException("Cannot accrue interest on a closed account");
        var firstOfNextMonth = new DateOnly(year, month, 1).AddMonths(1);
        if (LastAccrualDate is { } last && firstOfNextMonth <= last)
            throw new OperationNotPermittedException($"Interest already accrued for or after {year:D4}-{month:D2}");

        var monthlyRate = AnnualInterestRate / MonthsPerYear;
        var interestAmount = Math.Round(Balance.Amount * monthlyRate, 2, MidpointRounding.AwayFromZero);
        var interest = new Money(interestAmount, Currency);
        Balance = Balance.Add(interest);
        LastAccrualDate = firstOfNextMonth;
        return Transaction.Create(Id, TransactionType.INTEREST, interest, $"Interest accrual for {year:D4}-{month:D2}");
    }
}
