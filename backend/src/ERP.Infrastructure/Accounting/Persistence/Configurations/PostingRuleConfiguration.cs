using ERP.Domain.Modules.Accounting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Accounting.Persistence.Configurations;

public sealed class PostingRuleConfiguration : IEntityTypeConfiguration<PostingRule>
{
    public void Configure(EntityTypeBuilder<PostingRule> builder)
    {
        builder.ToTable("posting_rules");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder
            .Property(x => x.SourceModule)
            .HasColumnName("source_module")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(x => x.FactType).HasColumnName("fact_type").HasMaxLength(100).IsRequired();

        // Columnas planas, sin FK a Account (ADR-026 §6.2 — PostingRule no referencia el
        // aggregate Account; la existencia/pertenencia a la Company se valida en el Posting
        // Engine, no aquí).
        builder.Property(x => x.DebitAccountId).HasColumnName("debit_account_id");
        builder.Property(x => x.CreditAccountId).HasColumnName("credit_account_id");
        builder.Property(x => x.TaxCode).HasColumnName("tax_code").HasMaxLength(10);

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // Único — ADR-026 §8 ("fail closed: si no hay PostingRule configurada para un
        // PostingFact, no se genera ningún asiento") presupone resolución determinística: a lo
        // sumo una regla por (Company, SourceModule, FactType). Mismo criterio que
        // uq_pricing_rules_list_item (único sin distinguir IsActive, fuerza deshabilitar/
        // reactivar en vez de duplicar filas). Decisión revisada y confirmada por Database
        // Architecture Review Board antes de la primera migración de Accounting.
        builder
            .HasIndex(x => new
            {
                x.CompanyId,
                x.SourceModule,
                x.FactType,
            })
            .IsUnique()
            .HasDatabaseName("uq_posting_rules_company_source_fact");

        // PostingRuleLine — persistencia agregada en Fase 3.5.4. Cascade: una línea de mapeo no
        // tiene sentido de existir sin su regla — borrar la regla borra sus líneas.
        builder
            .HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.PostingRuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
