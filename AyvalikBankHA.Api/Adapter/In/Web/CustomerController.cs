using AyvalikBankHA.Api.Adapter.In.Web.Dto;
using AyvalikBankHA.Api.Domain.Port.In;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AyvalikBankHA.Api.Adapter.In.Web;

[ApiController]
[Route("api/customers")]
[Authorize(Roles = "CUSTOMER")]
public class CustomerController(IChangePasswordUseCase changePassword) : ControllerBase
{
    [HttpPut("{id:guid}/password")]
    public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordRequest req)
    {
        await changePassword.ChangePasswordAsync(new IChangePasswordUseCase.Command(id, req.NewPassword));
        return Ok();
    }
}
