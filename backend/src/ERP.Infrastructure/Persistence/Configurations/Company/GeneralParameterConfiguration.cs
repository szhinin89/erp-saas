using ERP.Domain.Modules.Company.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.CompanyConfig;

public class GeneralParameterConfiguration : IEntityTypeConfiguration<GeneralParameter>
{
    public void Configure(EntityTypeBuilder<GeneralParameter> builder)
    {
        builder.ToTable("general_parameter");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        // Multi-tenant scope (backfilled from company.subscriber_id)
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Value).HasColumnName("value").HasColumnType("text");
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(300);

        builder.HasIndex(x => x.SubscriberId).HasDatabaseName("ix_general_parameter_subscriber_id");
        builder.HasIndex(x => new { x.CompanyId, x.Key }).IsUnique().HasDatabaseName("uq_gen_param");

        builder.HasOne(x => x.Company)
            .WithMany(x => x.Parameters)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
