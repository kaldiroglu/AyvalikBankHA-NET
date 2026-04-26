namespace AyvalikBankHA.Api.Domain.Model;

public class Customer
{
    public Guid Id { get; }
    public string Name { get; }
    public string Email { get; }
    public string Role { get; }
    public string CurrentPasswordHash { get; private set; }

    public Customer(Guid id, string name, string email, string role, string currentPasswordHash)
    {
        Id = id;
        Name = name;
        Email = email;
        Role = role;
        CurrentPasswordHash = currentPasswordHash;
    }

    public static Customer Create(string name, string email, string passwordHash) =>
        new(Guid.NewGuid(), name, email, "CUSTOMER", passwordHash);

    public void ChangePassword(string newHash)
    {
        if (string.IsNullOrEmpty(newHash)) throw new ArgumentException("Hash must not be empty");
        CurrentPasswordHash = newHash;
    }
}
