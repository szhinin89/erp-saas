using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.ElectronicDocuments;

public sealed class ElectronicDocumentSriMessageConfiguration : IEntityTypeConfiguration<ElectronicDocumentSriMessage>
{
    public void Configure(EntityTypeBuilder<ElectronicDocumentSriMessage> builder)
    {
        builder.ConfigureAuditBase("electronic_document_sri_message");

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(10);
        builder.Property(x => x.MessageType).HasColumnName("message_type").HasMaxLength(50).IsRequired();

        // Mismo motivo BD-02 ya documentado en ElectronicDocumentAuditConfiguration: el texto
        // real de <mensaje>/<informacionAdicional> del SRI puede superar 500 caracteres —
        // PostgreSQL no trunca un varchar(N) excedido, lanza 22001 y pierde el mensaje justo en
        // el caso que más importa (rechazo real del SRI).
        builder.Property(x => x.Message).HasColumnName("message").IsRequired();
        builder.Property(x => x.AdditionalInfo).HasColumnName("additional_info");

        builder.HasIndex(x => new { x.TenantId, x.EntityId, x.OccurredAtUtc })
            .HasDatabaseName("ix_electronic_document_sri_message_entity_occurred_at");
    }
}
