using AyvalikBankHA.Api.Domain.Model;
using AyvalikBankHA.Api.Domain.Service;
using FluentAssertions;
using Xunit;

namespace AyvalikBankHA.Tests;

public class TransferDomainServiceTests
{
    private readonly TransferDomainService _service = new();

    [Fact]
    public void SameCustomerIsFree()
    {
        _service.CalculateFee(new Money(200m, Currency.USD), true, 1.0m, CustomerTier.STANDARD)
            .Amount.Should().Be(0m);
    }

    [Fact]
    public void StandardTierAppliesFullPercent()
    {
        _service.CalculateFee(new Money(200m, Currency.USD), false, 1.0m, CustomerTier.STANDARD)
            .Amount.Should().Be(2m);
    }

    [Fact]
    public void PremiumTierAppliesHalfPercent()
    {
        _service.CalculateFee(new Money(200m, Currency.USD), false, 1.0m, CustomerTier.PREMIUM)
            .Amount.Should().Be(1m);
    }

    [Fact]
    public void PrivateTierIsFree()
    {
        _service.CalculateFee(new Money(10_000m, Currency.USD), false, 1.0m, CustomerTier.PRIVATE)
            .Amount.Should().Be(0m);
    }

    [Fact]
    public void StandardTransferOverCapThrows()
    {
        var act = () => _service.RequireTransferWithinLimit(
            new Money(5_001m, Currency.USD), CustomerTier.STANDARD);
        act.Should().Throw<InvalidOperationException>().WithMessage("*5000*");
    }

    [Fact]
    public void StandardTransferAtCapPasses()
    {
        var act = () => _service.RequireTransferWithinLimit(
            new Money(5_000m, Currency.USD), CustomerTier.STANDARD);
        act.Should().NotThrow();
    }

    [Fact]
    public void PremiumTransferOverCapThrows()
    {
        var act = () => _service.RequireTransferWithinLimit(
            new Money(50_001m, Currency.USD), CustomerTier.PREMIUM);
        act.Should().Throw<InvalidOperationException>().WithMessage("*50000*");
    }

    [Fact]
    public void PrivateTransferHasNoCap()
    {
        var act = () => _service.RequireTransferWithinLimit(
            new Money(10_000_000m, Currency.USD), CustomerTier.PRIVATE);
        act.Should().NotThrow();
    }

    [Fact]
    public void StandardWithdrawalOverCapThrows()
    {
        var act = () => _service.RequireWithdrawalWithinLimit(
            new Money(5_001m, Currency.USD), CustomerTier.STANDARD);
        act.Should().Throw<InvalidOperationException>().WithMessage("*5000*");
    }

    [Fact]
    public void PrivateWithdrawalHasNoCap()
    {
        var act = () => _service.RequireWithdrawalWithinLimit(
            new Money(10_000_000m, Currency.USD), CustomerTier.PRIVATE);
        act.Should().NotThrow();
    }
}
