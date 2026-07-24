using ERP.Domain.Modules.Finance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Finance;

public sealed class CreditInstallmentConfiguration : IEntityTypeConfiguration<CreditInstallment>
{
    public void Configure(EntityTypeBuilder<CreditInstallment> builder)
    {
        builder.ToTable("credit_installments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.CreditTermId).HasColumnName("credit_term_id").IsRequired();
        builder.Property(x => x.InstallmentNumber).HasColumnName("installment_number").IsRequired();
        builder.Property(x => x.DaysOffset).HasColumnName("days_offset").IsRequired();
        builder.Property(x => x.Percentage).HasColumnName("percentage")
            .HasColumnType("numeric(5,2)").IsRequired();

        builder.HasIndex(x => new { x.CreditTermId, x.InstallmentNumber })
            .IsUnique()
            .HasDatabaseName("uq_credit_installments_term_number");
    }
}
