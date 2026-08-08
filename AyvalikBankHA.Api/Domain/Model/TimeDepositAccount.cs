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

    public override Transaction Deposit(TransactionAmount amount) =>
        throw new OperationNotPermittedException("Time deposit principal is locked — further deposits are not allowed");

    public override Transaction TransferOut(TransactionAmount amount, Money fee, Guid targetAccountId) =>
        throw new OperationNotPermittedException("Time deposit accounts do not support transfers");

    public override Transaction Withdraw(TransactionAmount amount)
    {
        RequireOperable();
        if (!Matured) throw new OperationNotPermittedException("Time deposit has not matured");
        RequireSameCurrency(amount);
        if (!Balance.IsGreaterThanOrEqualTo(amount.Value))
            throw new InsufficientBalanceException("Insufficient funds");
        Balance = Balance.Subtract(amount.Value);
        return Transaction.Create(Id, TransactionType.WITHDRAWAL, amount.Value, "Withdrawal");
    }

    public Transaction Mature(DateOnly today)
    {
        // FROZEN accounts can still mature: it's a date-driven system action.
        if (IsTerminal) throw new AccountNotActiveException("Cannot mature a closed account");
        if (Matured) throw new OperationNotPermittedException("Account is already matured");
        if (today < MaturityDate) throw new OperationNotPermittedException("Maturity date not yet reached");

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
