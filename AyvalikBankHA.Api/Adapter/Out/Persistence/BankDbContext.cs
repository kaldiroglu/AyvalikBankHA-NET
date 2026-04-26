using AyvalikBankHA.Api.Adapter.Out.Persistence.Entity;
using Microsoft.EntityFrameworkCore;

namespace AyvalikBankHA.Api.Adapter.Out.Persistence;

public class BankDbContext(DbContextOptions<BankDbContext> options) : DbContext(options)
{
    public DbSet<CustomerJpaEntity> Customers => Set<CustomerJpaEntity>();
    public DbSet<AccountJpaEntity> Accounts => Set<AccountJpaEntity>();
    public DbSet<TransactionJpaEntity> Transactions => Set<TransactionJpaEntity>();
    public DbSet<SettingsJpaEntity> Settings => Set<SettingsJpaEntity>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<CustomerJpaEntity>(b =>
        {
            b.ToTable("customers");
            b.HasKey(c => c.Id);
            b.HasIndex(c => c.Email).IsUnique();
        });

        mb.Entity<AccountJpaEntity>(b =>
        {
            b.ToTable("accounts");
            b.HasKey(a => a.Id);
            b.Property(a => a.Currency).HasMaxLength(8);
            b.Property(a => a.Status).HasMaxLength(16);
            b.Property(a => a.Balance).HasColumnType("numeric(19,2)");
        });

        mb.Entity<TransactionJpaEntity>(b =>
        {
            b.ToTable("transactions");
            b.HasKey(t => t.Id);
            b.Property(t => t.Type).HasMaxLength(16);
            b.Property(t => t.Currency).HasMaxLength(8);
            b.Property(t => t.Amount).HasColumnType("numeric(19,2)");
        });

        mb.Entity<SettingsJpaEntity>(b =>
        {
            b.ToTable("settings");
            b.HasKey(s => s.Key);
        });
    }
}
