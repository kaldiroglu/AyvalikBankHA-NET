using AyvalikBankHA.Api.Domain.Model;
using FluentAssertions;
using Xunit;

namespace AyvalikBankHA.Tests;

/// <summary>
/// TransactionAmount is strictly positive by construction, so no downstream method needs to check.
/// Money stays signed because a CheckingAccount balance legitimately goes negative under overdraft.
/// Mirrors AyvalikBankHA-JAVA Refactorings.md entry 1.
/// </summary>
public class TransactionAmountTests
{
    [Fact]
    public void Accepts_a_positive_amount()
    {
        var a = TransactionAmount.Of(100m, Currency.USD);

        a.Value.Amount.Should().Be(100m);
        a.Currency.Should().Be(Currency.USD);
    }

    [Theory]
    [InlineData(-50)]
    [InlineData(0)]
    public void Rejects_non_positive_amounts(decimal amount)
    {
        var act = () => TransactionAmount.Of(amount, Currency.USD);

        act.Should().Throw<ArgumentException>().WithMessage("*must be positive*");
    }

    [Fact]
    public void Money_itself_still_allows_negatives_so_overdraft_keeps_working()
    {
        var overdrawn = new Money(-500m, Currency.USD);

        overdrawn.Amount.Should().Be(-500m);
    }

    [Fact]
    public void Is_a_class_not_a_struct_so_there_is_no_default_backdoor()
    {
        // A readonly record struct would allow default(TransactionAmount), bypassing validation
        // entirely and yielding a zero amount. A class has no such parameterless escape hatch.
        typeof(TransactionAmount).IsClass.Should().BeTrue();
    }
}
