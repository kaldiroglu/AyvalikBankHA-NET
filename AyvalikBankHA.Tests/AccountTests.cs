using AyvalikBankHA.Api.Domain.Model;
using FluentAssertions;
using Xunit;

namespace AyvalikBankHA.Tests;

public class AccountTests
{
    private static Account NewUsd() => Account.Open(Guid.NewGuid(), Currency.USD);

    [Fact]
    public void OpensAtZeroAndActive()
    {
        var a = NewUsd();
        a.Balance.Amount.Should().Be(0m);
        a.Status.Should().Be(AccountStatus.ACTIVE);
    }

    [Fact]
    public void DepositRaisesBalanceAndReturnsTransaction()
    {
        var a = NewUsd();
        var tx = a.Deposit(new Money(500m, Currency.USD));
        a.Balance.Amount.Should().Be(500m);
        tx.Type.Should().Be(TransactionType.DEPOSIT);
    }

    [Fact]
    public void WithdrawDecreasesBalance()
    {
        var a = NewUsd();
        a.Deposit(new Money(500m, Currency.USD));
        a.Withdraw(new Money(200m, Currency.USD));
        a.Balance.Amount.Should().Be(300m);
    }

    [Fact]
    public void WithdrawingMoreThanBalanceThrows()
    {
        var a = NewUsd();
        a.Deposit(new Money(100m, Currency.USD));
        var act = () => a.Withdraw(new Money(200m, Currency.USD));
        act.Should().Throw<InvalidOperationException>().WithMessage("*Insufficient*");
    }

    [Fact]
    public void DepositInWrongCurrencyThrows()
    {
        var a = NewUsd();
        var act = () => a.Deposit(new Money(100m, Currency.EUR));
        act.Should().Throw<ArgumentException>().WithMessage("*currency*");
    }

    [Fact]
    public void FreezeBlocksDeposit()
    {
        var a = NewUsd();
        a.Freeze();
        var act = () => a.Deposit(new Money(100m, Currency.USD));
        act.Should().Throw<InvalidOperationException>().WithMessage("*frozen*");
    }

    [Fact]
    public void CloseIsTerminal()
    {
        var a = NewUsd();
        a.Close();
        var act = () => a.Freeze();
        act.Should().Throw<InvalidOperationException>().WithMessage("*closed*");
    }

    [Fact]
    public void TransferOutWithFeeDeductsTotal()
    {
        var a = NewUsd();
        a.Deposit(new Money(1000m, Currency.USD));
        a.TransferOut(new Money(200m, Currency.USD), new Money(2m, Currency.USD), Guid.NewGuid());
        a.Balance.Amount.Should().Be(798m);
    }
}
