using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Caja;

public sealed class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
{
    public void Configure(EntityTypeBuilder<CashSession> builder)
    {
        builder.ToTable("cash_sessions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();

        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(x => x.CashRegisterId).HasColumnName("cash_register_id").IsRequired();
        builder
            .Property(x => x.CashRegisterCodeSnapshot)
            .HasColumnName("cash_register_code_snapshot")
            .HasMaxLength(CashSession.CashRegisterCodeSnapshotMaxLen)
            .IsRequired();
        builder
            .Property(x => x.CashRegisterNameSnapshot)
            .HasColumnName("cash_register_name_snapshot")
            .HasMaxLength(CashSession.CashRegisterNameSnapshotMaxLen)
            .IsRequired();

        builder.Property(x => x.EmissionPointId).HasColumnName("emission_point_id").IsRequired();
        builder
            .Property(x => x.EmissionPointCodeSnapshot)
            .HasColumnName("emission_point_code_snapshot")
            .HasMaxLength(CashSession.EmissionPointCodeSnapshotMaxLen)
            .IsRequired();

        builder.Property(x => x.OpenedAt).HasColumnName("opened_at").IsRequired();
        builder
            .Property(x => x.OpeningAmount)
            .HasColumnName("opening_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(CashSession.NotesMaxLen);

        // ── Cierre ──────────────────────────────────────────────────
        builder.Property(x => x.ClosedAt).HasColumnName("closed_at");
        builder.Property(x => x.ClosedBy).HasColumnName("closed_by");
        builder
            .Property(x => x.CloseNotes)
            .HasColumnName("close_notes")
            .HasMaxLength(CashSession.CloseNotesMaxLen);

        builder
            .Property(x => x.ExpectedAmount)
            .HasColumnName("expected_amount")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.CountedAmount)
            .HasColumnName("counted_amount")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.Difference)
            .HasColumnName("difference")
            .HasColumnType("numeric(18,2)");

        // ── Auditoría ───────────────────────────────────────────────
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── Concurrency token (PostgreSQL xid) ──────────────────────
        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // ── Computed properties (NOT persisted) ─────────────────────
        builder.Ignore(x => x.TotalIncome);
        builder.Ignore(x => x.TotalExpense);
        builder.Ignore(x => x.CurrentBalance);

        // ── Relationships ───────────────────────────────────────────
        builder
            .HasMany(x => x.Movements)
            .WithOne()
            .HasForeignKey(x => x.CashSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.ClosingCounts)
            .WithOne()
            .HasForeignKey(x => x.CashSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<EmissionPoint>()
            .WithMany()
            .HasForeignKey(x => x.EmissionPointId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<CashRegister>()
            .WithMany()
            .HasForeignKey(x => x.CashRegisterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ─────────────────────────────────────────────────
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_cash_sessions_tenant_company");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.UserId,
                x.Status,
            })
            .HasDatabaseName("ix_cash_sessions_tenant_user_status");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CashRegisterId,
                x.Status,
            })
            .HasDatabaseName("ix_cash_sessions_tenant_register_status");

        builder
            .HasIndex(x => new { x.TenantId, x.EmissionPointId })
            .HasDatabaseName("ix_cash_sessions_tenant_ep");

        builder
            .HasIndex(x => new { x.TenantId, x.Status })
            .HasDatabaseName("ix_cash_sessions_tenant_status");
    }
}
