using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.ElectronicDocuments;

public sealed class ElectronicDocumentAuditConfiguration : IEntityTypeConfiguration<ElectronicDocumentAudit>
{
    public void Configure(EntityTypeBuilder<ElectronicDocumentAudit> builder)
    {
        builder.ConfigureAuditBase("electronic_document_audit");

        // BD-02 (auditoría SRI): override específico de este dominio sobre la columna "reason"
        // heredada de AuditRecordBase (varchar(500) por defecto — ver
        // AuditEntityConfigurationExtensions.ConfigureAuditBase, FROZEN, compartida por todos
        // los dominios de auditoría, no se modifica). Los mensajes reales del SRI se construyen
        // como "[identificador] mensaje: informacionAdicional" y pueden concatenarse varios por
        // respuesta (ver SriSoapClient.ParseRecepcionResponse/ParseAutorizacionResponse),
        // superando fácilmente los 500 caracteres — PostgreSQL no trunca un varchar(N) excedido,
        // lanza 22001 y pierde la fila de auditoría justo en el caso que más importa (rechazo
        // real del SRI). Solo esta tabla queda sin límite; el resto de dominios de auditoría no
        // se ve afectado.
        builder.Property(x => x.Reason).Metadata.SetMaxLength(null);

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.DocumentType).HasColumnName("document_type").HasConversion<int>().IsRequired();
        builder.Property(x => x.FromState).HasColumnName("from_state").HasConversion<int?>();
        builder.Property(x => x.ToState).HasColumnName("to_state").HasConversion<int>().IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.OccurredAtUtc })
            .HasDatabaseName("ix_electronic_document_audit_company_occurred_at");
    }
}
