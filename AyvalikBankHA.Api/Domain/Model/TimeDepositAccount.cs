namespace AyvalikBankHA.Api.Domain.Model;

public sealed class TimeDepositAccount : Account
{
    public Money Principal { get; }
    public DateOnly OpenedOn { get; }
    public DateOnly MaturityDate { get; }
    public decimal AnnualInterestRate { get; }
    public bool Matured { get; private set; }

    public TimeDepositAccount(Guid id, Guid ownerId, Currency currency,
        Money balance, AccountStatus status,
        Money principal, DateOnly openedOn, DateOnly maturityDate,
        decimal annualInterestRate, bool matured)
        : base(id, ownerId, currency, balance, status)
    {
        if (principal.Currency != currency)
            throw new ArgumentException("Principal currency must match account currency");
        if (principal.Amount <= 0) throw new ArgumentException("Principal must be positive");
        if (annualInterestRate < 0) throw new ArgumentException("Annual interest rate must be non-negative");
        if (maturityDate <= openedOn) throw new ArgumentException("Maturity date must be after opened-on date");
        Principal = principal;
        OpenedOn = openedOn;
        MaturityDate = maturityDate;
        AnnualInterestRate = annualInterestRate;
        Matured = matured;
    }

    public static TimeDepositAccount Open(Guid ownerId, Currency currency, Money principal,
        DateOnly maturityDate, decimal annualInterestRate)
    {
        var openedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        return new TimeDepositAccount(Guid.NewGuid(), ownerId, currency, principal,
            AccountStatus.ACTIVE, principal, openedOn, maturityDate, annualInterestRate, false);
    }

    public override AccountType Type => AccountType.TIME_DEPOSIT;

    public override Transaction Deposit(Money amount) =>
        throw new InvalidOperationException("Time deposit principal is locked — further deposits are not allowed");

    public override Transaction TransferOut(Money amount, Money fee, Guid targetAccountId) =>
        throw new InvalidOperationException("Time deposit accounts do not support transfers");

    public override Transaction Withdraw(Money amount)
    {
        RequireOperable();
        if (!Matured) throw new InvalidOperationException("Time deposit has not matured");
        RequireSameCurrency(amount);
        if (amount.Amount <= 0) throw new ArgumentException("Withdrawal amount must be positive");
        if (!Balance.IsGreaterThanOrEqualTo(amount))
            throw new InvalidOperationException("Insufficient funds");
        Balance = Balance.Subtract(amount);
        return Transaction.Create(Id, TransactionType.WITHDRAWAL, amount, "Withdrawal");
    }

    public Transaction Mature(DateOnly today)
    {
        // FROZEN accounts can still mature: it's a date-driven system action.
        if (IsTerminal) throw new InvalidOperationException("Cannot mature a closed account");
        if (Matured) throw new InvalidOperationException("Account is already matured");
        if (today < MaturityDate) throw new InvalidOperationException("Maturity date not yet reached");

        var months = (MaturityDate.Year - OpenedOn.Year) * 12 + (MaturityDate.Month - OpenedOn.Month);
        var years = (decimal)months / 12;
        var interestAmount = Math.Round(Principal.Amount * AnnualInterestRate * years,
            2, MidpointRounding.AwayFromZero);
        var interest = new Money(interestAmount, Currency);
        Balance = Balance.Add(interest);
        Matured = true;
        return Transaction.Create(Id, TransactionType.INTEREST, interest, "Maturity interest credit");
    }
}
