using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public sealed class PurchaseCommunicationConfiguration
    : IEntityTypeConfiguration<PurchaseCommunication>
{
    public void Configure(EntityTypeBuilder<PurchaseCommunication> builder)
    {
        builder.ToTable("purchase_communications");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.PurchaseId).HasColumnName("purchase_id").IsRequired();
        builder
            .Property(x => x.Subject)
            .HasColumnName("subject")
            .HasMaxLength(PurchaseCommunication.SubjectMaxLen)
            .IsRequired();
        builder
            .Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(PurchaseCommunication.NotesMaxLen);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(x => x.ScheduledDate).HasColumnName("scheduled_date");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => x.PurchaseId).HasDatabaseName("ix_purchase_communications_purchase");
    }
}
