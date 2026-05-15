using ERP.Domain.Modules.Auxiliary.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Auxiliary;

public class WsLogConfiguration : IEntityTypeConfiguration<WsLog>
{
    public void Configure(EntityTypeBuilder<WsLog> builder)
    {
        builder.ToTable("ws_log");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.DocId).HasColumnName("doc_id");
        builder.Property(x => x.Operation).HasColumnName("operation").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Environment).HasColumnName("environment");
        builder.Property(x => x.EndpointUrl).HasColumnName("endpoint_url").HasMaxLength(500);
        builder.Property(x => x.RequestPayload).HasColumnName("request_payload").HasColumnType("text");
        builder.Property(x => x.ResponsePayload).HasColumnName("response_payload").HasColumnType("text");
        builder.Property(x => x.HttpStatus).HasColumnName("http_status");
        builder.Property(x => x.DurationMs).HasColumnName("duration_ms");
        builder.Property(x => x.Success).HasColumnName("success");
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(10);
        builder.Property(x => x.ErrorDetail).HasColumnName("error_detail").HasColumnType("text");
        builder.Property(x => x.CalledAt).HasColumnName("called_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(x => new { x.CompanyId, x.CalledAt })
            .HasDatabaseName("idx_wslog_company");
        builder.HasIndex(x => x.DocId)
            .HasFilter("doc_id IS NOT NULL")
            .HasDatabaseName("idx_wslog_doc");
    }
}
