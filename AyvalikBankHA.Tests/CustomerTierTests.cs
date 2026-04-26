using AyvalikBankHA.Api.Domain.Model;
using FluentAssertions;
using Xunit;

namespace AyvalikBankHA.Tests;

public class CustomerTierTests
{
    [Fact]
    public void StandardTierHasFullFeeAndFiveThousandCaps()
    {
        CustomerTier.STANDARD.FeeMultiplier().Should().Be(1.00m);
        CustomerTier.STANDARD.MaxPerTransfer().Should().Be(5_000m);
        CustomerTier.STANDARD.MaxPerWithdrawal().Should().Be(5_000m);
    }

    [Fact]
    public void PremiumTierHasHalfFeeAndHigherCaps()
    {
        CustomerTier.PREMIUM.FeeMultiplier().Should().Be(0.50m);
        CustomerTier.PREMIUM.MaxPerTransfer().Should().Be(50_000m);
        CustomerTier.PREMIUM.MaxPerWithdrawal().Should().Be(25_000m);
    }

    [Fact]
    public void PrivateTierHasNoFeeAndNoCaps()
    {
        CustomerTier.PRIVATE.FeeMultiplier().Should().Be(0.00m);
        CustomerTier.PRIVATE.MaxPerTransfer().Should().BeNull();
        CustomerTier.PRIVATE.MaxPerWithdrawal().Should().BeNull();
    }

    [Fact]
    public void NewCustomerDefaultsToStandard()
    {
        var c = Customer.Create("Alice", "alice@example.com", "hash");
        c.Tier.Should().Be(CustomerTier.STANDARD);
    }

    [Fact]
    public void ChangeTierUpdatesCustomer()
    {
        var c = Customer.Create("Alice", "alice@example.com", "hash");
        c.ChangeTier(CustomerTier.PRIVATE);
        c.Tier.Should().Be(CustomerTier.PRIVATE);
    }
}
