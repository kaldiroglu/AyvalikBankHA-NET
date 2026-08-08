namespace AyvalikBankHA.Api.Domain.Model;

/// <summary>
/// A <b>strictly positive</b> monetary amount — the magnitude of a requested money movement.
///
/// <para><see cref="Money"/> deliberately allows negatives: a <see cref="CheckingAccount"/> balance
/// goes negative under overdraft. It therefore cannot enforce positivity, and every method taking an
/// amount re-asserted the rule by hand. Making the constraint a property of the <i>type</i> means it
/// is checked once, at construction, and every method downstream can simply trust it.</para>
///
/// <para><b>Zero is rejected as well as negative.</b> Direction is already carried by which
/// operation was called, so a signed amount is meaningless — and a zero-value transfer would write
/// two ledger rows recording no movement of money.</para>
///
/// <para>Wraps <see cref="Money"/> rather than re-implementing it, so all arithmetic and the
/// same-currency guard stay in one place. Balances, transfer fees and recorded transaction amounts
/// keep using <c>Money</c>, because zero is legal for all three.</para>
///
/// <para>Mirrors AyvalikBankHA-JAVA Refactorings.md entry 1. Note that a C# <c>readonly record
/// struct</c> always has a parameterless default — <c>default(TransactionAmount)</c> bypasses this
/// validation — so it is declared as a class, which has no such backdoor.</para>
/// </summary>
public sealed class TransactionAmount
{
    public Money Value { get; }

    private TransactionAmount(Money value)
    {
        if (value.Amount <= 0m)
            throw new ArgumentException($"Transaction amount must be positive, was {value.Amount}");
        Value = value;
    }

    public static TransactionAmount Of(Money money) => new(money);

    public static TransactionAmount Of(decimal amount, Currency currency) =>
        new(new Money(amount, currency));

    public Currency Currency => Value.Currency;

    public override string ToString() => $"{Value.Amount} {Value.Currency}";
}
