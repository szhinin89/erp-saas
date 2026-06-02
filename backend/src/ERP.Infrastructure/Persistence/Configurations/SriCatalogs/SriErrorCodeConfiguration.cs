using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriErrorCodeConfiguration : IEntityTypeConfiguration<SriErrorCode>
{
    public void Configure(EntityTypeBuilder<SriErrorCode> builder)
    {
        builder.ToTable("sri_error_code", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(10);
        builder.Property(x => x.ErrorType).HasColumnName("error_type").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.HasData(
            new SriErrorCode { Code = "35",  ErrorType = "ERROR",   Name = "CLAVE DE ACCESO REGISTRADA",         IsActive = true },
            new SriErrorCode { Code = "43",  ErrorType = "ERROR",   Name = "XML NO CUMPLE ESPECIFICACIONES",     IsActive = true },
            new SriErrorCode { Code = "60",  ErrorType = "WARNING", Name = "FIRMA INVÃLIDA",                     IsActive = true },
            new SriErrorCode { Code = "65",  ErrorType = "ERROR",   Name = "AMBIENTE NO VÃLIDO",                 IsActive = true },
            new SriErrorCode { Code = "70",  ErrorType = "ERROR",   Name = "CLAVE DE ACCESO NO REGISTRADA",      IsActive = true },
            new SriErrorCode { Code = "72",  ErrorType = "ERROR",   Name = "NÃšMERO DE COMPROBANTE YA EXISTE",    IsActive = true },
            new SriErrorCode { Code = "73",  ErrorType = "ERROR",   Name = "CLAVE DE ACCESO INVÃLIDA",           IsActive = true },
            new SriErrorCode { Code = "90",  ErrorType = "ERROR",   Name = "CERTIFICADO INVÃLIDO O CADUCADO",    IsActive = true },
            new SriErrorCode { Code = "102", ErrorType = "ERROR",   Name = "CLAVE DE ACCESO NO EXISTE",          IsActive = true },
            new SriErrorCode { Code = "300", ErrorType = "WARNING", Name = "COMPROBANTE NO AUTORIZADO",          IsActive = true },
            new SriErrorCode { Code = "301", ErrorType = "ERROR",   Name = "CLAVE DE ACCESO INCORRECTA",         IsActive = true }
        );
    }
}
