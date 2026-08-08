using AyvalikBankHA.Api.Adapter.In.Web.Dto;
using AyvalikBankHA.Api.Domain.Port.In;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AyvalikBankHA.Api.Adapter.In.Web;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
public class AdminController(
    IAccountAdministrationPort accountAdministration,
    ICustomerAdministrationPort customerAdministration,
    IBankSettingsPort bankSettings) : ControllerBase
{
    [HttpPost("customers")]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest req)
    {
        var c = await customerAdministration.CreateCustomerAsync(
            new ICustomerAdministrationPort.CreateCustomerCommand(req.Name, req.Email, req.Password));
        return StatusCode(201, CustomerResponse.From(c));
    }

    [HttpDelete("customers/{id:guid}")]
    public async Task<IActionResult> DeleteCustomer(Guid id)
    {
        await customerAdministration.DeleteCustomerAsync(id);
        return NoContent();
    }

    [HttpGet("customers")]
    public async Task<IActionResult> List()
    {
        var list = await customerAdministration.ListCustomersAsync();
        return Ok(list.Select(CustomerResponse.From));
    }

    [HttpPut("customers/{id:guid}/tier")]
    public async Task<IActionResult> ChangeTier(Guid id, [FromBody] ChangeCustomerTierRequest req)
    {
        await customerAdministration.ChangeCustomerTierAsync(new ICustomerAdministrationPort.ChangeCustomerTierCommand(id, req.Tier));
        return Ok();
    }

    [HttpPut("settings/transfer-fee")]
    public async Task<IActionResult> SetTransferFee([FromBody] SetTransferFeeRequest req)
    {
        await bankSettings.SetTransferFeeAsync(new IBankSettingsPort.SetTransferFeeCommand(req.FeePercent));
        return Ok();
    }

    [HttpPut("accounts/{id:guid}/freeze")]
    public async Task<IActionResult> Freeze(Guid id) { await accountAdministration.FreezeAccountAsync(id); return Ok(); }

    [HttpPut("accounts/{id:guid}/unfreeze")]
    public async Task<IActionResult> Unfreeze(Guid id) { await accountAdministration.UnfreezeAccountAsync(id); return Ok(); }

    [HttpPut("accounts/{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id) { await accountAdministration.CloseAccountAsync(id); return Ok(); }

    [HttpPut("accounts/{id:guid}/accrue-interest")]
    public async Task<IActionResult> AccrueInterest(Guid id, [FromBody] AccrueInterestRequest req)
    {
        var tx = await accountAdministration.AccrueInterestAsync(new IAccountAdministrationPort.AccrueInterestCommand(id, req.Year, req.Month));
        return Ok(TransactionResponse.From(tx));
    }

    [HttpPut("accounts/{id:guid}/mature")]
    public async Task<IActionResult> Mature(Guid id)
    {
        var tx = await accountAdministration.MatureAsync(new IAccountAdministrationPort.MatureCommand(id));
        return Ok(TransactionResponse.From(tx));
    }
}
