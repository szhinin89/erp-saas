using ERP.Domain.Modules.Menu.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class FuncionalidadConfiguration : IEntityTypeConfiguration<Funcionalidad>
{
    public void Configure(EntityTypeBuilder<Funcionalidad> builder)
    {
        builder.ToTable("funcionalidades");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(Funcionalidad.NombreMaxLen).IsRequired();
        builder.Property(x => x.Icono).HasColumnName("icono").HasMaxLength(Funcionalidad.IconoMaxLen);
        builder.Property(x => x.Ruta).HasColumnName("ruta").HasMaxLength(Funcionalidad.RutaMaxLen);
        builder.Property(x => x.Permiso).HasColumnName("permiso").HasMaxLength(Funcionalidad.PermisoMaxLen).IsRequired();
        builder.Property(x => x.PadreId).HasColumnName("padre_id");
        builder.Property(x => x.Orden).HasColumnName("orden").IsRequired();
        builder.Property(x => x.VisibleEnMenu).HasColumnName("visible_en_menu").IsRequired();
        builder.Property(x => x.EsSuperAdmin).HasColumnName("es_super_admin").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasIndex(x => x.Permiso).IsUnique().HasDatabaseName("ux_funcionalidades_permiso");
        builder.HasIndex(x => x.PadreId).HasDatabaseName("ix_funcionalidades_padre_id");

        builder.HasOne<Funcionalidad>()
            .WithMany()
            .HasForeignKey(x => x.PadreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
