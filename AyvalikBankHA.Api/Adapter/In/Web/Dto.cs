using System.ComponentModel.DataAnnotations;
using AyvalikBankHA.Api.Domain.Model;

namespace AyvalikBankHA.Api.Adapter.In.Web.Dto;

public record CreateCustomerRequest(
    [Required, StringLength(100)] string Name,
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record ChangePasswordRequest([Required] string NewPassword);

public record CreateCheckingAccountRequest(
    [Required] Currency Currency,
    decimal? OverdraftLimit);

public record CreateSavingsAccountRequest(
    [Required] Currency Currency,
    [Required, Range(typeof(decimal), "0", "10", ParseLimitsInInvariantCulture = true)] decimal AnnualInterestRate);

public record CreateTimeDepositAccountRequest(
    [Required] Currency Currency,
    [Required, Range(typeof(decimal), "0.01", "999999999", ParseLimitsInInvariantCulture = true)] decimal Principal,
    [Required] DateOnly MaturityDate,
    [Required, Range(typeof(decimal), "0", "10", ParseLimitsInInvariantCulture = true)] decimal AnnualInterestRate);

public record AccrueInterestRequest(
    [Required, Range(2000, 2100)] int Year,
    [Required, Range(1, 12)] int Month);

public record MoneyOperationRequest(
    [Required, Range(typeof(decimal), "0.01", "999999999", ParseLimitsInInvariantCulture = true)] decimal Amount,
    [Required] Currency Currency);

public record TransferRequest(
    [Required] Guid TargetAccountId,
    [Required, Range(typeof(decimal), "0.01", "999999999", ParseLimitsInInvariantCulture = true)] decimal Amount,
    [Required] Currency Currency);

public record SetTransferFeeRequest(
    [Required, Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)] decimal FeePercent);

public record ChangeCustomerTierRequest([Required] CustomerTier Tier);

public record CustomerResponse(Guid Id, string Name, string Email, string Role, string Tier)
{
    public static CustomerResponse From(Customer c) =>
        new(c.Id, c.Name, c.Email, c.Role, c.Tier.ToString());
}

public record AccountResponse(
    Guid Id, Guid OwnerId, string Currency, decimal Balance, string Status, string Type,
    decimal? OverdraftLimit, decimal? InterestRate, DateOnly? LastAccrualDate,
    decimal? Principal, DateOnly? OpenedOn, DateOnly? MaturityDate, bool? Matured)
{
    public static AccountResponse From(Account a) => a switch
    {
        CheckingAccount c => new AccountResponse(c.Id, c.OwnerId, c.Currency.ToString(),
            c.Balance.Amount, c.Status.ToString(), "CHECKING",
            c.OverdraftLimit.Amount, null, null, null, null, null, null),
        SavingsAccount s => new AccountResponse(s.Id, s.OwnerId, s.Currency.ToString(),
            s.Balance.Amount, s.Status.ToString(), "SAVINGS",
            null, s.AnnualInterestRate, s.LastAccrualDate, null, null, null, null),
        TimeDepositAccount t => new AccountResponse(t.Id, t.OwnerId, t.Currency.ToString(),
            t.Balance.Amount, t.Status.ToString(), "TIME_DEPOSIT",
            null, t.AnnualInterestRate, null, t.Principal.Amount, t.OpenedOn, t.MaturityDate, t.Matured),
        _ => throw new InvalidOperationException($"Unknown Account subtype: {a.GetType().Name}")
    };
}

public record BalanceResponse(decimal Amount, string Currency)
{
    public static BalanceResponse From(Money m) => new(m.Amount, m.Currency.ToString());
}

public record TransactionResponse(
    Guid Id, Guid AccountId, string Type, decimal Amount, string Currency,
    DateTimeOffset CreatedAt, string Description)
{
    public static TransactionResponse From(Transaction t) =>
        new(t.Id, t.AccountId, t.Type.ToString(), t.Amount.Amount, t.Amount.Currency.ToString(),
            t.Timestamp, t.Description);
}
