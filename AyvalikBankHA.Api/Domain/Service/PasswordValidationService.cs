namespace AyvalikBankHA.Api.Domain.Service;

// Pure-Java domain service. Zero framework imports.
public class PasswordValidationService
{
    public void Validate(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password must not be empty");
        if (password.Length < 8 || password.Length > 16)
            throw new ArgumentException("Password length must be between 8 and 16");
        if (!password.Any(char.IsUpper))
            throw new ArgumentException("Password must contain at least one uppercase letter");
        if (!password.Any(char.IsLower))
            throw new ArgumentException("Password must contain at least one lowercase letter");
        if (!password.Any(char.IsDigit))
            throw new ArgumentException("Password must contain at least one digit");
        if (!password.Any(c => "!@#$%^&*()_+-=[]{}|;:,.<>?/~`".Contains(c)))
            throw new ArgumentException("Password must contain at least one special character");
    }
}
