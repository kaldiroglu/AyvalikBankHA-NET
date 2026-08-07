using AyvalikBankHA.Api.Adapter.In.Web.Dto;
using AyvalikBankHA.Api.Domain.Port.In;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AyvalikBankHA.Api.Adapter.In.Web;

[ApiController]
[Route("api/customers")]
[Authorize(Roles = "CUSTOMER")]
public class CustomerController(IChangePasswordUseCase changePassword) : ControllerBase
{
    /// <summary>
    /// The authenticated customer's id, taken from the ClaimTypes.NameIdentifier claim that
    /// BasicAuthHandler sets at authentication time. Authorization must never trust an id supplied
    /// by the caller in a route or query string.
    /// </summary>
    private Guid CallerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPut("{id:guid}/password")]
    public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordRequest req)
    {
        await changePassword.ChangePasswordAsync(new IChangePasswordUseCase.Command(CallerId, id, req.NewPassword));
        return Ok();
    }
}
