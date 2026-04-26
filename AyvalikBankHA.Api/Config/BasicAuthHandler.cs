using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using AyvalikBankHA.Api.Domain.Port.Out;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AyvalikBankHA.Api.Config;

public class BasicAuthOptions : AuthenticationSchemeOptions { }

public class BasicAuthHandler : AuthenticationHandler<BasicAuthOptions>
{
    public const string SchemeName = "Basic";
    private readonly IServiceScopeFactory _scopeFactory;

    public BasicAuthHandler(
        IOptionsMonitor<BasicAuthOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        IServiceScopeFactory scopeFactory) : base(options, loggerFactory, encoder)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers["Authorization"].FirstOrDefault();
        if (header is null || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        string username, password;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
            var i = raw.IndexOf(':');
            if (i < 0) return AuthenticateResult.Fail("Malformed Basic header");
            username = raw[..i];
            password = raw[(i + 1)..];
        }
        catch
        {
            return AuthenticateResult.Fail("Malformed Basic header");
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var customerRepo = scope.ServiceProvider.GetRequiredService<ICustomerRepositoryPort>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherPort>();
        var customer = await customerRepo.FindByEmailAsync(username);
        if (customer is null || !hasher.Matches(password, customer.CurrentPasswordHash))
            return AuthenticateResult.Fail("Invalid credentials");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new Claim(ClaimTypes.Name, customer.Email),
            new Claim(ClaimTypes.Role, customer.Role)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers["WWW-Authenticate"] = $"Basic realm=\"AyvalikBank\", charset=\"UTF-8\"";
        Response.StatusCode = 401;
        return Task.CompletedTask;
    }
}
