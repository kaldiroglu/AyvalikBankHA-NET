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
    ICreateAccountUseCase createAccount,
    IDepositMoneyUseCase depositMoney,
    IWithdrawMoneyUseCase withdrawMoney,
    ITransferMoneyUseCase transferMoney,
    IGetBalanceUseCase getBalance,
    IGetTransactionsUseCase getTransactions,
    IListAccountsUseCase listAccounts) : ControllerBase
{
    [HttpPost("accounts")]
    public async Task<IActionResult> Create([FromQuery] Guid ownerId, [FromBody] CreateAccountRequest req)
    {
        var a = await createAccount.CreateAccountAsync(new ICreateAccountUseCase.Command(ownerId, req.Currency));
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
