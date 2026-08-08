using AyvalikBankHA.Api.Domain.Model;
using FluentAssertions;
using Xunit;

namespace AyvalikBankHA.Tests;

public class SavingsAccountTests
{
    [Fact]
    public void OpensWithGivenInterestRate()
    {
        var a = SavingsAccount.Open(Guid.NewGuid(), Currency.USD, 0.06m);
        a.Type.Should().Be(AccountType.SAVINGS);
        a.AnnualInterestRate.Should().Be(0.06m);
        a.LastAccrualDate.Should().BeNull();
    }

    [Fact]
    public void NegativeInterestRateRejected()
    {
        var act = () => SavingsAccount.Open(Guid.NewGuid(), Currency.USD, -0.01m);
        act.Should().Throw<ArgumentException>().WithMessage("*non-negative*");
    }

    [Fact]
    public void WithdrawCannotGoNegative()
    {
        var a = SavingsAccount.Open(Guid.NewGuid(), Currency.USD, 0.05m);
        a.Deposit(TransactionAmount.Of(50m, Currency.USD));
        var act = () => a.Withdraw(TransactionAmount.Of(60m, Currency.USD));
        act.Should().Throw<InvalidOperationException>().WithMessage("*Insufficient*");
    }

    [Fact]
    public void AccrueInterestAddsMonthlyInterest()
    {
        var a = SavingsAccount.Open(Guid.NewGuid(), Currency.USD, 0.12m);
        a.Deposit(TransactionAmount.Of(1_000m, Currency.USD));
        var tx = a.AccrueInterest(2026, 4);
        // 12% annual / 12 = 1% monthly → 10.00 on 1000
        a.Balance.Amount.Should().Be(1_010m);
        tx.Type.Should().Be(TransactionType.INTEREST);
        tx.Amount.Amount.Should().Be(10m);
        a.LastAccrualDate.Should().Be(new DateOnly(2026, 5, 1));
    }

    [Fact]
    public void AccrueInterestForSameMonthRejected()
    {
        var a = SavingsAccount.Open(Guid.NewGuid(), Currency.USD, 0.12m);
        a.Deposit(TransactionAmount.Of(1_000m, Currency.USD));
        a.AccrueInterest(2026, 4);
        var act = () => a.AccrueInterest(2026, 4);
        act.Should().Throw<InvalidOperationException>().WithMessage("*already accrued*");
    }

    [Fact]
    public void AccrueInterestOnClosedRejected()
    {
        var a = SavingsAccount.Open(Guid.NewGuid(), Currency.USD, 0.05m);
        a.Close();
        var act = () => a.AccrueInterest(2026, 4);
        act.Should().Throw<InvalidOperationException>().WithMessage("*closed*");
    }

    [Fact]
    public void AccrueOnFrozenStillWorks()
    {
        var a = SavingsAccount.Open(Guid.NewGuid(), Currency.USD, 0.12m);
        a.Deposit(TransactionAmount.Of(1_000m, Currency.USD));
        a.Freeze();
        var tx = a.AccrueInterest(2026, 4);
        tx.Amount.Amount.Should().Be(10m);
    }
}
