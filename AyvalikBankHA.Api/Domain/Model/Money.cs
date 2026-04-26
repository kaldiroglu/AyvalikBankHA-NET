namespace AyvalikBankHA.Api.Domain.Model;

// Value object: amount + currency, with arithmetic and same-currency guard.
public readonly record struct Money(decimal Amount, Currency Currency)
{
    public static Money Zero(Currency c) => new(0m, c);

    public Money Add(Money other)
    {
        Require(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        Require(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public bool IsGreaterThanOrEqualTo(Money other)
    {
        Require(other);
        return Amount >= other.Amount;
    }

    private void Require(Money other)
    {
        if (other.Currency != Currency)
            throw new ArgumentException($"Currency mismatch: {Currency} vs {other.Currency}");
    }
}
