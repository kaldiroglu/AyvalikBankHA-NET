using AyvalikBankHA.Api.Domain.Model;
using FluentAssertions;
using Xunit;

namespace AyvalikBankHA.Tests;

public class AccountStateTests
{
    private static CheckingAccount NewActive() => CheckingAccount.Open(Guid.NewGuid(), Currency.USD);

    [Fact]
    public void NewAccountIsActive()
    {
        var a = NewActive();
        a.Status.Should().Be(AccountStatus.ACTIVE);
        a.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void FreezeMovesToFrozen()
    {
        var a = NewActive();
        a.Freeze();
        a.Status.Should().Be(AccountStatus.FROZEN);
    }

    [Fact]
    public void UnfreezeMovesFrozenToActive()
    {
        var a = NewActive();
        a.Freeze();
        a.Unfreeze();
        a.Status.Should().Be(AccountStatus.ACTIVE);
    }

    [Fact]
    public void FreezingFrozenThrows()
    {
        var a = NewActive();
        a.Freeze();
        var act = () => a.Freeze();
        act.Should().Throw<InvalidOperationException>().WithMessage("*already frozen*");
    }

    [Fact]
    public void UnfreezingActiveThrows()
    {
        var a = NewActive();
        var act = () => a.Unfreeze();
        act.Should().Throw<InvalidOperationException>().WithMessage("*not frozen*");
    }

    [Fact]
    public void CloseFromActiveIsTerminal()
    {
        var a = NewActive();
        a.Close();
        a.Status.Should().Be(AccountStatus.CLOSED);
        a.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void CloseFromFrozenIsTerminal()
    {
        var a = NewActive();
        a.Freeze();
        a.Close();
        a.Status.Should().Be(AccountStatus.CLOSED);
    }

    [Fact]
    public void ClosedRejectsAllTransitions()
    {
        var a = NewActive();
        a.Close();
        ((Action)(() => a.Freeze())).Should().Throw<InvalidOperationException>().WithMessage("*closed*");
        ((Action)(() => a.Unfreeze())).Should().Throw<InvalidOperationException>().WithMessage("*closed*");
        ((Action)(() => a.Close())).Should().Throw<InvalidOperationException>().WithMessage("*already closed*");
    }

    [Fact]
    public void FrozenBlocksDeposit()
    {
        var a = NewActive();
        a.Freeze();
        var act = () => a.Deposit(TransactionAmount.Of(100m, Currency.USD));
        act.Should().Throw<InvalidOperationException>().WithMessage("*frozen*");
    }

    [Fact]
    public void FrozenBlocksWithdraw()
    {
        var a = NewActive();
        a.Deposit(TransactionAmount.Of(100m, Currency.USD));
        a.Freeze();
        var act = () => a.Withdraw(TransactionAmount.Of(50m, Currency.USD));
        act.Should().Throw<InvalidOperationException>().WithMessage("*frozen*");
    }

    [Fact]
    public void ClosedBlocksDeposit()
    {
        var a = NewActive();
        a.Close();
        var act = () => a.Deposit(TransactionAmount.Of(100m, Currency.USD));
        act.Should().Throw<InvalidOperationException>().WithMessage("*closed*");
    }
}
