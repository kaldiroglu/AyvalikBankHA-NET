using AyvalikBankHA.Api.Domain.Model;
using FluentAssertions;
using Xunit;

namespace AyvalikBankHA.Tests;

public class MoneyTests
{
    [Fact]
    public void AddsSameCurrency()
    {
        new Money(100m, Currency.USD).Add(new Money(50m, Currency.USD)).Amount.Should().Be(150m);
    }

    [Fact]
    public void RejectsAddDifferentCurrency()
    {
        var act = () => new Money(100m, Currency.USD).Add(new Money(50m, Currency.EUR));
        act.Should().Throw<ArgumentException>().WithMessage("*Currency mismatch*");
    }

    [Fact]
    public void GteWorksWithinSameCurrency()
    {
        new Money(100m, Currency.USD).IsGreaterThanOrEqualTo(new Money(100m, Currency.USD)).Should().BeTrue();
        new Money(99m, Currency.USD).IsGreaterThanOrEqualTo(new Money(100m, Currency.USD)).Should().BeFalse();
    }

    [Fact]
    public void ZeroIsZero()
    {
        Money.Zero(Currency.TRY).Amount.Should().Be(0m);
        Money.Zero(Currency.TRY).Currency.Should().Be(Currency.TRY);
    }
}
