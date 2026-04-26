using AyvalikBankHA.Api.Domain.Port.Out;

namespace AyvalikBankHA.Api.Adapter.Out.Security;

public class BCryptPasswordHasherAdapter : IPasswordHasherPort
{
    public string Hash(string raw) => BCrypt.Net.BCrypt.HashPassword(raw, workFactor: 12);
    public bool Matches(string raw, string hash) => BCrypt.Net.BCrypt.Verify(raw, hash);
}
