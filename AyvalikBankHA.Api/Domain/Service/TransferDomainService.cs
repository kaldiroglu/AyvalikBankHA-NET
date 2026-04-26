using AyvalikBankHA.Api.Domain.Model;

namespace AyvalikBankHA.Api.Domain.Service;

public class TransferDomainService
{
    public Money CalculateFee(Money amount, bool sameCustomer, decimal feePercent)
    {
        if (sameCustomer) return Money.Zero(amount.Currency);
        var fee = Math.Round(amount.Amount * feePercent / 100m, 2, MidpointRounding.AwayFromZero);
        return new Money(fee, amount.Currency);
    }
}
