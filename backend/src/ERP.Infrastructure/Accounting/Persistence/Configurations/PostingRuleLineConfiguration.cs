using ERP.Domain.Modules.Accounting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Accounting.Persistence.Configurations;

public sealed class PostingRuleLineConfiguration : IEntityTypeConfiguration<PostingRuleLine>
{
    public void Configure(EntityTypeBuilder<PostingRuleLine> builder)
    {
        builder.ToTable("posting_rule_lines");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.PostingRuleId).HasColumnName("posting_rule_id").IsRequired();

        // Columna plana, sin FK a Account — mismo criterio que PostingRule.DebitAccountId/
        // CreditAccountId (ADR-026 §6.2): configuración de datos, existencia/pertenencia a la
        // Company se valida en Application/Infrastructure al momento de resolver.
        builder.Property(x => x.AccountId).HasColumnName("account_id").IsRequired();

        builder.Property(x => x.Nature).HasColumnName("nature").HasConversion<int>().IsRequired();
        builder
            .Property(x => x.AmountKind)
            .HasColumnName("amount_kind")
            .HasConversion<int>()
            .IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();

        builder
            .HasIndex(x => x.PostingRuleId)
            .HasDatabaseName("ix_posting_rule_lines_posting_rule");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_posting_rule_lines_tenant");
    }
}
