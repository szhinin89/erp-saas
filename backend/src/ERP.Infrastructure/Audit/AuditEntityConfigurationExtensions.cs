using ERP.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Audit;

/// <summary>
/// Mapeo EF de las columnas comunes de <see cref="AuditRecordBase"/> — cada configuración
/// de auditoría de dominio la invoca una sola vez en vez de repetir el mapeo de las 10
/// columnas base. Los índices de (TenantId, EntityId, OccurredAtUtc) y
/// (TenantId, UserId, OccurredAtUtc) cubren las consultas comunes de la Regla de
/// Consultas (entidad/usuario/fecha/acción); cada dominio agrega sus propios índices
/// específicos (por producto, cliente, cuenta, documento, etc.) además de estos.
/// </summary>
public static class AuditEntityConfigurationExtensions
{
    public static void ConfigureAuditBase<TAudit>(this EntityTypeBuilder<TAudit> builder, string tableName)
        where TAudit : AuditRecordBase
    {
        builder.ToTable(tableName);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.EntityId).HasColumnName("entity_id").IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(80).IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.UserName).HasColumnName("user_name").HasMaxLength(254).IsRequired();
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
        builder.Property(x => x.RequestId).HasColumnName("request_id").HasMaxLength(100);
        builder.Property(x => x.Source).HasColumnName("source").HasConversion<int>().IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.EntityId, x.OccurredAtUtc })
            .HasDatabaseName($"ix_{tableName}_entity_occurred_at");
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.OccurredAtUtc })
            .HasDatabaseName($"ix_{tableName}_user_occurred_at");
    }
}
