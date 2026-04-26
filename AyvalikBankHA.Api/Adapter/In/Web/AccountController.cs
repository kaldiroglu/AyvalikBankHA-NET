using AyvalikBankHA.Api.Adapter.In.Web.Dto;
using AyvalikBankHA.Api.Domain.Model;
using AyvalikBankHA.Api.Domain.Port.In;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AyvalikBankHA.Api.Adapter.In.Web;

[ApiController]
[Route("api")]
[Authorize(Roles = "CUSTOMER")]
public class AccountController(
    IOpenCheckingAccountUseCase openChecking,
    IOpenSavingsAccountUseCase openSavings,
    IOpenTimeDepositAccountUseCase openTimeDeposit,
    IDepositMoneyUseCase depositMoney,
    IWithdrawMoneyUseCase withdrawMoney,
    ITransferMoneyUseCase transferMoney,
    IGetBalanceUseCase getBalance,
    IGetTransactionsUseCase getTransactions,
    IListAccountsUseCase listAccounts) : ControllerBase
{
    [HttpPost("accounts/checking")]
    public async Task<IActionResult> CreateChecking([FromQuery] Guid ownerId, [FromBody] CreateCheckingAccountRequest req)
    {
        var od = new Money(req.OverdraftLimit ?? 0m, req.Currency);
        var a = await openChecking.OpenCheckingAsync(new IOpenCheckingAccountUseCase.Command(ownerId, req.Currency, od));
        return StatusCode(201, AccountResponse.From(a));
    }

    [HttpPost("accounts/savings")]
    public async Task<IActionResult> CreateSavings([FromQuery] Guid ownerId, [FromBody] CreateSavingsAccountRequest req)
    {
        var a = await openSavings.OpenSavingsAsync(new IOpenSavingsAccountUseCase.Command(ownerId, req.Currency, req.AnnualInterestRate));
        return StatusCode(201, AccountResponse.From(a));
    }

    [HttpPost("accounts/time-deposit")]
    public async Task<IActionResult> CreateTimeDeposit([FromQuery] Guid ownerId, [FromBody] CreateTimeDepositAccountRequest req)
    {
        var a = await openTimeDeposit.OpenTimeDepositAsync(new IOpenTimeDepositAccountUseCase.Command(
            ownerId, req.Currency, new Money(req.Principal, req.Currency), req.MaturityDate, req.AnnualInterestRate));
        return StatusCode(201, AccountResponse.From(a));
    }

    [HttpGet("customers/{customerId:guid}/accounts")]
    public async Task<IActionResult> List(Guid customerId)
    {
        var accounts = await listAccounts.ListAccountsAsync(customerId);
        return Ok(accounts.Select(AccountResponse.From));
    }

    [HttpGet("accounts/{accountId:guid}/balance")]
    public async Task<IActionResult> GetBalance(Guid accountId)
    {
        var balance = await getBalance.GetBalanceAsync(accountId);
        return Ok(BalanceResponse.From(balance));
    }

    [HttpPost("accounts/{accountId:guid}/deposit")]
    public async Task<IActionResult> Deposit(Guid accountId, [FromBody] MoneyOperationRequest req)
    {
        var tx = await depositMoney.DepositAsync(new IDepositMoneyUseCase.Command(accountId,
            new Money(req.Amount, req.Currency)));
        return StatusCode(201, TransactionResponse.From(tx));
    }

    [HttpPost("accounts/{accountId:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid accountId, [FromBody] MoneyOperationRequest req)
    {
        var tx = await withdrawMoney.WithdrawAsync(new IWithdrawMoneyUseCase.Command(accountId,
            new Money(req.Amount, req.Currency)));
        return StatusCode(201, TransactionResponse.From(tx));
    }

    [HttpPost("accounts/{accountId:guid}/transfer")]
    public async Task<IActionResult> Transfer(Guid accountId, [FromBody] TransferRequest req)
    {
        await transferMoney.TransferAsync(new ITransferMoneyUseCase.Command(accountId, req.TargetAccountId,
            new Money(req.Amount, req.Currency)));
        return Ok();
    }

    [HttpGet("accounts/{accountId:guid}/transactions")]
    public async Task<IActionResult> GetTransactions(Guid accountId)
    {
        var txs = await getTransactions.GetTransactionsAsync(accountId);
        return Ok(txs.Select(TransactionResponse.From));
    }
}
