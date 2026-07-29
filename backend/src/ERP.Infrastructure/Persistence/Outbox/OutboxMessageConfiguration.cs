using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).IsRequired().HasMaxLength(500);

        // Clean event name (no assembly): used for routing, filtering, analytics, and event catalog.
        builder.Property(x => x.EventName).IsRequired().HasMaxLength(200);

        // Schema version — defaults to 1. Increment on non-additive payload changes.
        builder.Property(x => x.EventVersion).IsRequired().HasDefaultValue(1);

        builder.Property(x => x.Payload).IsRequired();

        // Optional metadata JSON: trace, correlation, AI tags, integration hints.
        builder.Property(x => x.MetadataJson).HasMaxLength(4000);

        builder.Property(x => x.OccurredOnUtc).IsRequired();

        builder.Property(x => x.Error).HasMaxLength(2000);

        builder.Property(x => x.TenantId);

        // Efficient polling: pending messages ordered by occurrence time
        builder
            .HasIndex(x => new { x.ProcessedOnUtc, x.OccurredOnUtc })
            .HasDatabaseName("IX_OutboxMessages_Pending");

        // Tenant-scoped analytics queries
        builder
            .HasIndex(x => new { x.TenantId, x.OccurredOnUtc })
            .HasDatabaseName("IX_OutboxMessages_Tenant");

        // EventName-based routing / analytics / catalog lookups
        builder
            .HasIndex(x => new { x.EventName, x.OccurredOnUtc })
            .HasDatabaseName("IX_OutboxMessages_EventName");
    }
}
