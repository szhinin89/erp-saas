using ERP.Domain.Modules.Communications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Communications;

public sealed class CommunicationOutboxAttachmentConfiguration
    : IEntityTypeConfiguration<CommunicationOutboxAttachment>
{
    public void Configure(EntityTypeBuilder<CommunicationOutboxAttachment> builder)
    {
        builder.ToTable("communication_outbox_attachments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.CommunicationOutboxId).HasColumnName("communication_outbox_id").IsRequired();
        builder.Property(x => x.AttachmentType).HasColumnName("attachment_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(CommunicationOutboxAttachment.FileNameMaxLen).IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(CommunicationOutboxAttachment.ContentTypeMaxLen).IsRequired();
        builder.Property(x => x.FileStoragePath).HasColumnName("file_storage_path").HasMaxLength(CommunicationOutboxAttachment.FileStoragePathMaxLen);
        builder.Property(x => x.BinaryContent).HasColumnName("binary_content");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => x.CommunicationOutboxId)
            .HasDatabaseName("ix_communication_outbox_attachments_outbox");
    }
}
