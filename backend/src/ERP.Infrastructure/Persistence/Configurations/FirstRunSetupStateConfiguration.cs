using ERP.Domain.Auth.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class FirstRunSetupStateConfiguration : IEntityTypeConfiguration<FirstRunSetupState>
{
    public void Configure(EntityTypeBuilder<FirstRunSetupState> builder)
    {
        builder.ToTable("first_run_setup_state");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IsFirstRun).HasColumnName("is_first_run").IsRequired();
        builder.Property(x => x.SetupTokenHash).HasColumnName("setup_token_hash").HasMaxLength(256);
        builder.Property(x => x.SetupTokenExpiryUtc).HasColumnName("setup_token_expiry_utc");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
    }
}
