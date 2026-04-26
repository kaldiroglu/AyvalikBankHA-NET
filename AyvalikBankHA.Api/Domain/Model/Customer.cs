namespace AyvalikBankHA.Api.Domain.Model;

public class Customer
{
    public Guid Id { get; }
    public string Name { get; }
    public string Email { get; }
    public string Role { get; }
    public CustomerTier Tier { get; private set; }
    public string CurrentPasswordHash { get; private set; }

    public Customer(Guid id, string name, string email, string role, CustomerTier tier, string currentPasswordHash)
    {
        Id = id;
        Name = name;
        Email = email;
        Role = role;
        Tier = tier;
        CurrentPasswordHash = currentPasswordHash;
    }

    public static Customer Create(string name, string email, string passwordHash) =>
        new(Guid.NewGuid(), name, email, "CUSTOMER", CustomerTier.STANDARD, passwordHash);

    public void ChangePassword(string newHash)
    {
        if (string.IsNullOrEmpty(newHash)) throw new ArgumentException("Hash must not be empty");
        CurrentPasswordHash = newHash;
    }

    public void ChangeTier(CustomerTier newTier) => Tier = newTier;
}
