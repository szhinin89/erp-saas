using Microsoft.EntityFrameworkCore;
using Modules.Accounting.Domain.Entities;
using Modules.Accounting.Infrastructure.Configurations;

namespace Modules.Accounting.Infrastructure.Persistence;

public class AccountingDbContext(DbContextOptions<AccountingDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new AccountConfiguration());
    }
}
