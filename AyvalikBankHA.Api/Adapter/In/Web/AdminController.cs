using AyvalikBankHA.Api.Adapter.In.Web.Dto;
using AyvalikBankHA.Api.Domain.Port.In;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AyvalikBankHA.Api.Adapter.In.Web;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
public class AdminController(
    ICreateCustomerUseCase createCustomer,
    IDeleteCustomerUseCase deleteCustomer,
    IListCustomersUseCase listCustomers,
    ISetTransferFeeUseCase setTransferFee,
    IFreezeAccountUseCase freezeAccount,
    IUnfreezeAccountUseCase unfreezeAccount,
    ICloseAccountUseCase closeAccount) : ControllerBase
{
    [HttpPost("customers")]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest req)
    {
        var c = await createCustomer.CreateCustomerAsync(
            new ICreateCustomerUseCase.Command(req.Name, req.Email, req.Password));
        return StatusCode(201, CustomerResponse.From(c));
    }

    [HttpDelete("customers/{id:guid}")]
    public async Task<IActionResult> DeleteCustomer(Guid id)
    {
        await deleteCustomer.DeleteCustomerAsync(id);
        return NoContent();
    }

    [HttpGet("customers")]
    public async Task<IActionResult> List()
    {
        var list = await listCustomers.ListCustomersAsync();
        return Ok(list.Select(CustomerResponse.From));
    }

    [HttpPut("settings/transfer-fee")]
    public async Task<IActionResult> SetTransferFee([FromBody] SetTransferFeeRequest req)
    {
        await setTransferFee.SetTransferFeeAsync(new ISetTransferFeeUseCase.Command(req.FeePercent));
        return Ok();
    }

    [HttpPut("accounts/{id:guid}/freeze")]
    public async Task<IActionResult> Freeze(Guid id) { await freezeAccount.FreezeAccountAsync(id); return Ok(); }

    [HttpPut("accounts/{id:guid}/unfreeze")]
    public async Task<IActionResult> Unfreeze(Guid id) { await unfreezeAccount.UnfreezeAccountAsync(id); return Ok(); }

    [HttpPut("accounts/{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id) { await closeAccount.CloseAccountAsync(id); return Ok(); }
}
