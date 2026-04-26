using AyvalikBankHA.Api.Adapter.Out.Persistence;
using AyvalikBankHA.Api.Adapter.Out.Persistence.Entity;
using AyvalikBankHA.Api.Domain.Port.Out;
using Microsoft.EntityFrameworkCore;

namespace AyvalikBankHA.Api.Config;

public static class AdminSeeder
{
    public const string AdminEmail = "admin@ayvalikbank.dev";
    public const string AdminPassword = "Admin@123!";

    public static async Task SeedAsync(BankDbContext db, IPasswordHasherPort hasher)
    {
        await db.Database.EnsureCreatedAsync();
        if (!await db.Customers.AnyAsync(c => c.Email == AdminEmail))
        {
            db.Customers.Add(new CustomerJpaEntity
            {
                Id = Guid.NewGuid(),
                Name = "System Admin",
                Email = AdminEmail,
                Role = "ADMIN",
                CurrentPassword = hasher.Hash(AdminPassword)
            });
        }
        if (!await db.Settings.AnyAsync(s => s.Key == "TRANSFER_FEE_PERCENT"))
        {
            db.Settings.Add(new SettingsJpaEntity { Key = "TRANSFER_FEE_PERCENT", Value = "1.0" });
        }
        await db.SaveChangesAsync();
    }
}
