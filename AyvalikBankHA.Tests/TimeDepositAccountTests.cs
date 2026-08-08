using AyvalikBankHA.Api.Domain.Model;
using FluentAssertions;
using Xunit;

namespace AyvalikBankHA.Tests;

public class TimeDepositAccountTests
{
    private static TimeDepositAccount NewOneYearUsd(decimal principal = 10_000m, decimal rate = 0.05m)
    {
        var maturity = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1);
        return TimeDepositAccount.Open(Guid.NewGuid(), Currency.USD,
            new Money(principal, Currency.USD), maturity, rate);
    }

    [Fact]
    public void OpensWithPrincipalAsBalance()
    {
        var a = NewOneYearUsd(5_000m, 0.04m);
        a.Type.Should().Be(AccountType.TIME_DEPOSIT);
        a.Balance.Amount.Should().Be(5_000m);
        a.Principal.Amount.Should().Be(5_000m);
        a.Matured.Should().BeFalse();
    }

    [Fact]
    public void DepositRejected()
    {
        var a = NewOneYearUsd();
        var act = () => a.Deposit(TransactionAmount.Of(100m, Currency.USD));
        act.Should().Throw<InvalidOperationException>().WithMessage("*locked*");
    }

    [Fact]
    public void TransferOutRejected()
    {
        var a = NewOneYearUsd();
        var act = () => a.TransferOut(
            TransactionAmount.Of(100m, Currency.USD), new Money(0m, Currency.USD), Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>().WithMessage("*do not support transfers*");
    }

    [Fact]
    public void WithdrawBeforeMaturityRejected()
    {
        var a = NewOneYearUsd();
        var act = () => a.Withdraw(TransactionAmount.Of(100m, Currency.USD));
        act.Should().Throw<InvalidOperationException>().WithMessage("*not matured*");
    }

    [Fact]
    public void MatureBeforeMaturityDateRejected()
    {
        var a = NewOneYearUsd();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var act = () => a.Mature(today);
        act.Should().Throw<InvalidOperationException>().WithMessage("*not yet reached*");
    }

    [Fact]
    public void MatureCreditsInterestAndAllowsWithdraw()
    {
        var a = NewOneYearUsd(10_000m, 0.05m);
        var matureDay = a.MaturityDate;
        var tx = a.Mature(matureDay);
        a.Matured.Should().BeTrue();
        tx.Type.Should().Be(TransactionType.INTEREST);
        // 10000 * 0.05 * 1 year = 500
        tx.Amount.Amount.Should().Be(500m);
        a.Balance.Amount.Should().Be(10_500m);

        a.Withdraw(TransactionAmount.Of(2_000m, Currency.USD));
        a.Balance.Amount.Should().Be(8_500m);
    }

    [Fact]
    public void MatureTwiceRejected()
    {
        var a = NewOneYearUsd();
        a.Mature(a.MaturityDate);
        var act = () => a.Mature(a.MaturityDate);
        act.Should().Throw<InvalidOperationException>().WithMessage("*already matured*");
    }

    [Fact]
    public void NonPositivePrincipalRejected()
    {
        var maturity = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1);
        var act = () => TimeDepositAccount.Open(Guid.NewGuid(), Currency.USD,
            new Money(0m, Currency.USD), maturity, 0.05m);
        act.Should().Throw<ArgumentException>().WithMessage("*positive*");
    }

    [Fact]
    public void MaturityDateBeforeOpenedOnRejected()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var act = () => TimeDepositAccount.Open(Guid.NewGuid(), Currency.USD,
            new Money(100m, Currency.USD), today, 0.05m);
        act.Should().Throw<ArgumentException>().WithMessage("*Maturity date*");
    }
}
