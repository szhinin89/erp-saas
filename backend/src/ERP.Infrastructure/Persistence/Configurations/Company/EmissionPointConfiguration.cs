using ERP.Domain.Modules.Company.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.CompanyConfig;

public class EmissionPointConfiguration : IEntityTypeConfiguration<EmissionPoint>
{
    public void Configure(EntityTypeBuilder<EmissionPoint> builder)
    {
        builder.ToTable("emission_point");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id").IsRequired();

        builder.Property(x => x.Code).HasColumnName("code")
            .HasMaxLength(EmissionPoint.CodeMaxLen).IsFixedLength().IsRequired();
        builder.Property(x => x.Name).HasColumnName("name")
            .HasMaxLength(EmissionPoint.NameMaxLen);
        builder.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => x.SubscriberId).HasDatabaseName("ix_emission_point_subscriber_id");
        builder.HasIndex(x => new { x.EstablishmentId, x.Code }).IsUnique().HasDatabaseName("uq_ep_code");

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Establishment)
            .WithMany(x => x.EmissionPoints)
            .HasForeignKey(x => x.EstablishmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
