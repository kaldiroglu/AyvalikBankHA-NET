using AyvalikBankHA.Api.Domain.Model;
using FluentAssertions;
using Xunit;

namespace AyvalikBankHA.Tests;

public class CheckingAccountTests
{
    [Fact]
    public void OpensWithoutOverdraftByDefault()
    {
        var a = CheckingAccount.Open(Guid.NewGuid(), Currency.USD);
        a.Type.Should().Be(AccountType.CHECKING);
        a.OverdraftLimit.Amount.Should().Be(0m);
    }

    [Fact]
    public void OpensWithOverdraftLimit()
    {
        var a = CheckingAccount.Open(Guid.NewGuid(), Currency.USD, new Money(500m, Currency.USD));
        a.OverdraftLimit.Amount.Should().Be(500m);
    }

    [Fact]
    public void WithdrawWithoutOverdraftRejectsOverdraw()
    {
        var a = CheckingAccount.Open(Guid.NewGuid(), Currency.USD);
        a.Deposit(TransactionAmount.Of(50m, Currency.USD));
        var act = () => a.Withdraw(TransactionAmount.Of(100m, Currency.USD));
        act.Should().Throw<InvalidOperationException>().WithMessage("*Insufficient*");
    }

    [Fact]
    public void WithdrawWithinOverdraftAllowsNegativeBalance()
    {
        var a = CheckingAccount.Open(Guid.NewGuid(), Currency.USD, new Money(200m, Currency.USD));
        a.Deposit(TransactionAmount.Of(50m, Currency.USD));
        a.Withdraw(TransactionAmount.Of(150m, Currency.USD));
        a.Balance.Amount.Should().Be(-100m);
    }

    [Fact]
    public void WithdrawBeyondOverdraftThrows()
    {
        var a = CheckingAccount.Open(Guid.NewGuid(), Currency.USD, new Money(100m, Currency.USD));
        var act = () => a.Withdraw(TransactionAmount.Of(101m, Currency.USD));
        act.Should().Throw<InvalidOperationException>().WithMessage("*overdraft*");
    }

    [Fact]
    public void OverdraftCurrencyMustMatch()
    {
        var act = () => new CheckingAccount(
            Guid.NewGuid(), Guid.NewGuid(), Currency.USD,
            Money.Zero(Currency.USD), AccountStatus.ACTIVE,
            new Money(100m, Currency.EUR));
        act.Should().Throw<ArgumentException>().WithMessage("*currency*");
    }

    [Fact]
    public void NegativeOverdraftRejected()
    {
        var act = () => new CheckingAccount(
            Guid.NewGuid(), Guid.NewGuid(), Currency.USD,
            Money.Zero(Currency.USD), AccountStatus.ACTIVE,
            new Money(-1m, Currency.USD));
        act.Should().Throw<ArgumentException>().WithMessage("*negative*");
    }
}
