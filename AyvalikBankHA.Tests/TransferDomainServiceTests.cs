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
        _service.CalculateFee(new Money(200m, Currency.USD), true, 1.0m).Amount.Should().Be(0m);
    }

    [Fact]
    public void CrossCustomerAppliesPercent()
    {
        _service.CalculateFee(new Money(200m, Currency.USD), false, 1.0m).Amount.Should().Be(2m);
    }
}
