using ERP.Domain.Modules.Employees.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Employees;

public sealed class EmployeeAuditConfiguration : IEntityTypeConfiguration<EmployeeAudit>
{
    public void Configure(EntityTypeBuilder<EmployeeAudit> builder)
    {
        builder.ConfigureAuditBase("employee_audit");

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();
        builder
            .Property(x => x.ChangedField)
            .HasColumnName("changed_field")
            .HasMaxLength(EmployeeAudit.MaxFieldLength);
        builder
            .Property(x => x.OldValue)
            .HasColumnName("old_value")
            .HasMaxLength(EmployeeAudit.MaxValueLength);
        builder
            .Property(x => x.NewValue)
            .HasColumnName("new_value")
            .HasMaxLength(EmployeeAudit.MaxValueLength);
    }
}

public sealed class EmployeeCatalogAuditConfiguration
    : IEntityTypeConfiguration<EmployeeCatalogAudit>
{
    public void Configure(EntityTypeBuilder<EmployeeCatalogAudit> builder)
    {
        builder.ConfigureAuditBase("employee_catalog_audit");

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.CatalogType)
            .HasColumnName("catalog_type")
            .HasConversion<int>()
            .IsRequired();
        builder
            .Property(x => x.OldName)
            .HasColumnName("old_name")
            .HasMaxLength(EmployeeCatalogEntry.MaxNameLength);
        builder
            .Property(x => x.NewName)
            .HasColumnName("new_name")
            .HasMaxLength(EmployeeCatalogEntry.MaxNameLength)
            .IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
    }
}
