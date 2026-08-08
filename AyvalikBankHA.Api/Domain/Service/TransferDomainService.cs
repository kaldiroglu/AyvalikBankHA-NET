using AyvalikBankHA.Api.Domain.Model;

namespace AyvalikBankHA.Api.Domain.Service;

public class TransferDomainService
{
    public Money CalculateFee(TransactionAmount amount, bool sameCustomer, decimal feePercent, CustomerTier sourceTier)
    {
        if (sameCustomer) return Money.Zero(amount.Currency);
        var scaledPercent = feePercent * sourceTier.FeeMultiplier();
        var fee = Math.Round(amount.Value.Amount * scaledPercent / 100m, 2, MidpointRounding.AwayFromZero);
        return new Money(fee, amount.Currency);
    }

    public void RequireTransferWithinLimit(TransactionAmount amount, CustomerTier tier)
    {
        var cap = tier.MaxPerTransfer();
        if (cap is not null && amount.Value.Amount > cap.Value)
            throw new TransactionLimitExceededException(
                $"Transfer amount {amount.Value.Amount} exceeds {tier} tier limit of {cap}");
    }

    public void RequireWithdrawalWithinLimit(TransactionAmount amount, CustomerTier tier)
    {
        var cap = tier.MaxPerWithdrawal();
        if (cap is not null && amount.Value.Amount > cap.Value)
            throw new TransactionLimitExceededException(
                $"Withdrawal amount {amount.Value.Amount} exceeds {tier} tier limit of {cap}");
    }
}
