using ERP.Domain.Modules.Cash.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Cash;

public sealed class CashCountConfiguration : IEntityTypeConfiguration<CashCount>
{
    public void Configure(EntityTypeBuilder<CashCount> builder)
    {
        builder.ToTable("cash_count");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.PettyCashId).HasColumnName("petty_cash_id").IsRequired();
        builder.Property(x => x.CountDate).HasColumnName("count_date").IsRequired();
        builder.Property(x => x.PhysicalCash).HasColumnName("physical_cash").HasPrecision(18, 2);
        builder.Property(x => x.Difference).HasColumnName("difference").HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(CashCount.NotesMaxLen);
        builder.Property(x => x.IsApproved).HasColumnName("is_approved").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<PettyCash>()
            .WithMany()
            .HasForeignKey(x => x.PettyCashId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
