using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Accounting.Domain.Entities;

namespace Modules.Accounting.Infrastructure.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Code).IsRequired().HasMaxLength(20);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(150);
        builder.Property(a => a.Type).IsRequired();
        builder.Property(a => a.Nature).IsRequired();
        builder.Property(a => a.Level).IsRequired();
        builder.Property(a => a.AllowsMovement).IsRequired();
        builder.Property(a => a.TenantId).IsRequired();
        builder.HasIndex(a => a.TenantId);
        builder.HasIndex(a => new { a.Code, a.TenantId }).IsUnique();
        builder.HasOne<Account>()
               .WithMany()
               .HasForeignKey(a => a.ParentId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
