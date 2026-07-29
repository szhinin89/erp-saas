using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public sealed class SriIdTypeUsageConfiguration : IEntityTypeConfiguration<SriIdTypeUsage>
{
    public void Configure(EntityTypeBuilder<SriIdTypeUsage> builder)
    {
        builder.ToTable("sri_id_type_usage", schema: "global");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.IdTypeCode).HasMaxLength(5).IsRequired();
        builder.Property(x => x.UsageType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder
            .HasOne<SriIdType>()
            .WithMany()
            .HasForeignKey(x => x.IdTypeCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => new { x.IdTypeCode, x.UsageType })
            .IsUnique()
            .HasDatabaseName("uq_sri_id_type_usage");

        // ── Seed data ────────────────────────────────────────────────────────
        builder.HasData(
            // 04 RUC → Customer, Supplier, Carrier
            new SriIdTypeUsage
            {
                Id = Guid.Parse("a0000001-0000-4000-9000-000000000001"),
                IdTypeCode = "04",
                UsageType = IdentificationUsageType.Customer,
            },
            new SriIdTypeUsage
            {
                Id = Guid.Parse("a0000001-0000-4000-9000-000000000002"),
                IdTypeCode = "04",
                UsageType = IdentificationUsageType.Supplier,
            },
            new SriIdTypeUsage
            {
                Id = Guid.Parse("a0000001-0000-4000-9000-000000000003"),
                IdTypeCode = "04",
                UsageType = IdentificationUsageType.Carrier,
            },
            // 05 Cédula → Customer, Employee, Carrier
            new SriIdTypeUsage
            {
                Id = Guid.Parse("a0000001-0000-4000-9000-000000000004"),
                IdTypeCode = "05",
                UsageType = IdentificationUsageType.Customer,
            },
            new SriIdTypeUsage
            {
                Id = Guid.Parse("a0000001-0000-4000-9000-000000000005"),
                IdTypeCode = "05",
                UsageType = IdentificationUsageType.Employee,
            },
            new SriIdTypeUsage
            {
                Id = Guid.Parse("a0000001-0000-4000-9000-000000000006"),
                IdTypeCode = "05",
                UsageType = IdentificationUsageType.Carrier,
            },
            // 06 Pasaporte → Customer, Employee
            new SriIdTypeUsage
            {
                Id = Guid.Parse("a0000001-0000-4000-9000-000000000007"),
                IdTypeCode = "06",
                UsageType = IdentificationUsageType.Customer,
            },
            new SriIdTypeUsage
            {
                Id = Guid.Parse("a0000001-0000-4000-9000-000000000008"),
                IdTypeCode = "06",
                UsageType = IdentificationUsageType.Employee,
            },
            // 07 Consumidor Final → Customer only
            new SriIdTypeUsage
            {
                Id = Guid.Parse("a0000001-0000-4000-9000-000000000009"),
                IdTypeCode = "07",
                UsageType = IdentificationUsageType.Customer,
            },
            // 08 Exterior → Customer, Supplier
            new SriIdTypeUsage
            {
                Id = Guid.Parse("a0000001-0000-4000-9000-00000000000a"),
                IdTypeCode = "08",
                UsageType = IdentificationUsageType.Customer,
            },
            new SriIdTypeUsage
            {
                Id = Guid.Parse("a0000001-0000-4000-9000-00000000000b"),
                IdTypeCode = "08",
                UsageType = IdentificationUsageType.Supplier,
            },
            // 09 Placa → Customer, Carrier
            new SriIdTypeUsage
            {
                Id = Guid.Parse("a0000001-0000-4000-9000-00000000000c"),
                IdTypeCode = "09",
                UsageType = IdentificationUsageType.Customer,
            },
            new SriIdTypeUsage
            {
                Id = Guid.Parse("a0000001-0000-4000-9000-00000000000d"),
                IdTypeCode = "09",
                UsageType = IdentificationUsageType.Carrier,
            }
        );
    }
}
