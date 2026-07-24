using ERP.Domain.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Setup;

public sealed class SystemSetupStateConfiguration : IEntityTypeConfiguration<SystemSetupState>
{
    public void Configure(EntityTypeBuilder<SystemSetupState> builder)
    {
        builder.ToTable("system_setup_state");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();
        builder.Property(x => x.IsInitialized)
               .HasColumnName("is_initialized")
               .HasDefaultValue(false)
               .IsRequired();
        builder.Property(x => x.InitializedAtUtc)
               .HasColumnName("initialized_at_utc");
        builder.Property(x => x.AdminEmail)
               .HasColumnName("admin_email")
               .HasMaxLength(256);
        builder.Property(x => x.IsFirstRun)
               .HasColumnName("is_first_run")
               .HasDefaultValue(true)
               .IsRequired();
        builder.Property(x => x.SetupTokenHash)
               .HasColumnName("setup_token_hash")
               .HasMaxLength(128);
        builder.Property(x => x.SetupTokenExpiryUtc)
               .HasColumnName("setup_token_expiry_utc");
    }
}
