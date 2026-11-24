using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Employees.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Employees;

/// <summary>
/// Mapeo EF común a todas las tablas auditables de Employees: columnas de identidad, tenant,
/// empresa, actor de mutación y token de concurrencia <c>xmin</c> (patrón de CashSession).
/// </summary>
internal static class EmployeeMapping
{
    public static void ConfigureCommon<T>(EntityTypeBuilder<T> builder, string table)
        where T : ERP.Domain.Common.AuditableEntity, ERP.Domain.Common.ICompanyOperationalEntity
    {
        builder.ToTable(table);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }

    public static void ConfigureCatalog<T>(
        EntityTypeBuilder<T> builder,
        string table,
        string uniqueName
    )
        where T : EmployeeCatalogEntry
    {
        ConfigureCommon(builder, table);
        builder
            .Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(EmployeeCatalogEntry.MaxNameLength)
            .IsRequired();
        builder
            .Property(x => x.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(EmployeeCatalogEntry.MaxNameLength)
            .IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Ignore(x => x.CatalogType);

        // UX-CAT-01
        builder
            .HasIndex(x => new { x.CompanyId, x.NormalizedName })
            .IsUnique()
            .HasDatabaseName(uniqueName);
    }
}

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        EmployeeMapping.ConfigureCommon(builder, "employees");

        builder.Property(x => x.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();
        builder.Property(x => x.IdentityUserId).HasColumnName("identity_user_id");
        builder.Property(x => x.JobPositionId).HasColumnName("job_position_id");
        builder.Property(x => x.ProfessionId).HasColumnName("profession_id");
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder
            .HasMany(x => x.Periods)
            .WithOne()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Periods).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.IdentityUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<JobPosition>()
            .WithMany()
            .HasForeignKey(x => x.JobPositionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<Profession>()
            .WithMany()
            .HasForeignKey(x => x.ProfessionId)
            .OnDelete(DeleteBehavior.Restrict);

        // UX-EMP-01
        builder
            .HasIndex(x => new { x.CompanyId, x.BusinessPartnerId })
            .IsUnique()
            .HasDatabaseName("uq_employees_company_business_partner");
        // UX-EMP-02
        builder
            .HasIndex(x => new { x.CompanyId, x.IdentityUserId })
            .IsUnique()
            .HasFilter("identity_user_id IS NOT NULL")
            .HasDatabaseName("uq_employees_company_identity_user");
        // IX-EMP-01: guardia de BP entre empresas del tenant (EMP-E3)
        builder
            .HasIndex(x => new { x.TenantId, x.BusinessPartnerId })
            .HasFilter("is_active")
            .HasDatabaseName("ix_employees_tenant_business_partner_active");
    }
}

public sealed class EmployeeEmploymentPeriodConfiguration
    : IEntityTypeConfiguration<EmployeeEmploymentPeriod>
{
    public void Configure(EntityTypeBuilder<EmployeeEmploymentPeriod> builder)
    {
        EmployeeMapping.ConfigureCommon(builder, "employee_employment_periods");

        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.Property(x => x.StartDate).HasColumnName("start_date").IsRequired();
        builder.Property(x => x.EndDate).HasColumnName("end_date");
        builder
            .Property(x => x.TerminationReason)
            .HasColumnName("termination_reason")
            .HasMaxLength(EmployeeEmploymentPeriod.MaxTerminationReasonLength);

        // CK-PER-01
        builder.ToTable(
            "employee_employment_periods",
            t =>
                t.HasCheckConstraint(
                    "ck_employee_employment_periods_dates",
                    "end_date IS NULL OR end_date >= start_date"
                )
        );

        // UX-PER-01 (EX-PER-01 es SQL crudo en la baseline)
        builder
            .HasIndex(x => x.EmployeeId)
            .IsUnique()
            .HasFilter("end_date IS NULL")
            .HasDatabaseName("uq_employee_employment_periods_open");
    }
}

public sealed class EmployeeBranchAssignmentConfiguration
    : IEntityTypeConfiguration<EmployeeBranchAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeBranchAssignment> builder)
    {
        EmployeeMapping.ConfigureCommon(builder, "employee_branch_assignments");

        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.StartDate).HasColumnName("start_date").IsRequired();
        builder.Property(x => x.EndDate).HasColumnName("end_date");

        // CK-ASG-01 (EX-ASG-01 es SQL crudo en la baseline)
        builder.ToTable(
            "employee_branch_assignments",
            t =>
                t.HasCheckConstraint(
                    "ck_employee_branch_assignments_dates",
                    "end_date IS NULL OR end_date >= start_date"
                )
        );

        builder
            .HasOne<Employee>()
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => new { x.EmployeeId, x.BranchId })
            .HasDatabaseName("ix_employee_branch_assignments_employee_branch");
        builder
            .HasIndex(x => new { x.TenantId, x.BranchId })
            .HasDatabaseName("ix_employee_branch_assignments_tenant_branch");
    }
}

public sealed class JobPositionConfiguration : IEntityTypeConfiguration<JobPosition>
{
    public void Configure(EntityTypeBuilder<JobPosition> builder) =>
        EmployeeMapping.ConfigureCatalog(
            builder,
            "employee_job_positions",
            "uq_employee_job_positions_company_name"
        );
}

public sealed class ProfessionConfiguration : IEntityTypeConfiguration<Profession>
{
    public void Configure(EntityTypeBuilder<Profession> builder) =>
        EmployeeMapping.ConfigureCatalog(
            builder,
            "employee_professions",
            "uq_employee_professions_company_name"
        );
}

public sealed class SpecialtyConfiguration : IEntityTypeConfiguration<Specialty>
{
    public void Configure(EntityTypeBuilder<Specialty> builder) =>
        EmployeeMapping.ConfigureCatalog(
            builder,
            "employee_specialties",
            "uq_employee_specialties_company_name"
        );
}

public sealed class EmployeeSpecialtyConfiguration : IEntityTypeConfiguration<EmployeeSpecialty>
{
    public void Configure(EntityTypeBuilder<EmployeeSpecialty> builder)
    {
        EmployeeMapping.ConfigureCommon(builder, "employee_specialty_links");

        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.Property(x => x.SpecialtyId).HasColumnName("specialty_id").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder
            .HasOne<Employee>()
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<Specialty>()
            .WithMany()
            .HasForeignKey(x => x.SpecialtyId)
            .OnDelete(DeleteBehavior.Restrict);

        // UX-SPL-01
        builder
            .HasIndex(x => new { x.EmployeeId, x.SpecialtyId })
            .IsUnique()
            .HasDatabaseName("uq_employee_specialty_links_employee_specialty");
        builder
            .HasIndex(x => x.SpecialtyId)
            .HasDatabaseName("ix_employee_specialty_links_specialty");
    }
}
