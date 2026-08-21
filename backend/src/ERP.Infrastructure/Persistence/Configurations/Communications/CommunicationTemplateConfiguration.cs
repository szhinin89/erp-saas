using ERP.Domain.Modules.Communications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Communications;

public sealed class CommunicationTemplateConfiguration : IEntityTypeConfiguration<CommunicationTemplate>
{
    public void Configure(EntityTypeBuilder<CommunicationTemplate> builder)
    {
        builder.ToTable("communication_templates");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(CommunicationTemplate.CodeMaxLen).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(CommunicationTemplate.NameMaxLen).IsRequired();
        builder.Property(x => x.Channel).HasColumnName("channel").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.SubjectTemplate).HasColumnName("subject_template").HasMaxLength(CommunicationTemplate.SubjectTemplateMaxLen).IsRequired();
        builder.Property(x => x.HtmlTemplate).HasColumnName("html_template").HasMaxLength(CommunicationTemplate.BodyTemplateMaxLen);
        builder.Property(x => x.TextTemplate).HasColumnName("text_template").HasMaxLength(CommunicationTemplate.BodyTemplateMaxLen);
        builder.Property(x => x.Language).HasColumnName("language").HasMaxLength(CommunicationTemplate.LanguageMaxLen).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.Channel, x.Code, x.Language })
            .IsUnique()
            .HasDatabaseName("ux_communication_templates_code_language");
    }
}
