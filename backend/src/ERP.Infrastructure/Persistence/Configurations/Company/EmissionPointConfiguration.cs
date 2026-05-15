using ERP.Domain.Modules.Company.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Company;

public class EmissionPointConfiguration : IEntityTypeConfiguration<EmissionPoint>
{
    public void Configure(EntityTypeBuilder<EmissionPoint> builder)
    {
        builder.ToTable("emission_point");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(x => new { x.EstablishmentId, x.Code }).IsUnique().HasDatabaseName("uq_ep_code");

        builder.HasOne(x => x.Company)
            .WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Establishment)
            .WithMany(x => x.EmissionPoints).HasForeignKey(x => x.EstablishmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
