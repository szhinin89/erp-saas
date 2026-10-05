using ERP.Domain.Modules.Communications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Communications;

/// <summary>
/// ZH-COMMUNICATIONS-CONTRACT-01 — historial de intentos (ADR-039 D11). Lo escribe solo
/// CommunicationOutboxDeliveryStore (SQL). Un intento por claim: (comunicación, número) y el
/// ClaimToken son únicos.
/// </summary>
public sealed class CommunicationDeliveryAttemptConfiguration
    : IEntityTypeConfiguration<CommunicationDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<CommunicationDeliveryAttempt> builder)
    {
        builder.ToTable("communication_delivery_attempts");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.CommunicationId).HasColumnName("communication_id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.AttemptNumber).HasColumnName("attempt_number").IsRequired();
        builder.Property(x => x.ClaimToken).HasColumnName("claim_token").IsRequired();
        builder.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc").IsRequired();
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder
            .Property(x => x.Transport)
            .HasColumnName("transport")
            .HasMaxLength(CommunicationDeliveryAttempt.TransportMaxLen)
            .IsRequired();
        builder
            .Property(x => x.Result)
            .HasColumnName("result")
            .HasConversion<string>()
            .HasMaxLength(20);
        builder
            .Property(x => x.FailureCategory)
            .HasColumnName("failure_category")
            .HasConversion<string>()
            .HasMaxLength(30);
        builder
            .Property(x => x.ProviderCode)
            .HasColumnName("provider_code")
            .HasMaxLength(CommunicationDeliveryAttempt.ProviderCodeMaxLen);
        builder
            .Property(x => x.ProviderMessageId)
            .HasColumnName("provider_message_id")
            .HasMaxLength(CommunicationDeliveryAttempt.ProviderMessageIdMaxLen);
        builder
            .Property(x => x.ErrorSafeText)
            .HasColumnName("error_safe_text")
            .HasMaxLength(CommunicationDeliveryAttempt.ErrorSafeTextMaxLen);

        builder
            .HasOne<CommunicationOutbox>()
            .WithMany()
            .HasForeignKey(x => x.CommunicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(x => new { x.CommunicationId, x.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("ux_communication_delivery_attempts_number");
        builder
            .HasIndex(x => x.ClaimToken)
            .IsUnique()
            .HasDatabaseName("ux_communication_delivery_attempts_claim_token");
    }
}
