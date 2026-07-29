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
        builder
            .Property(x => x.ErrorType)
            .HasColumnName("error_type")
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        // Fuente única: Ficha Técnica de Comprobantes Electrónicos Esquema Offline (docs/,
        // §11 "Códigos de errores y advertencias de validación") — verificado texto por texto
        // contra el PDF oficial (auditoría de cumplimiento SRI). Los códigos 72/73/90/102/300/301
        // que existían aquí antes no aparecen en ningún lugar del documento oficial bajo esos
        // números — se eliminaron por no ser códigos reales del SRI (catálogo no admite datos
        // fabricados, mismo criterio ya aplicado a los XSD embebidos).
        builder.HasData(
            new SriErrorCode
            {
                Code = "27",
                ErrorType = "ERROR",
                Name = "CLASE NO PERMITIDO",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "28",
                ErrorType = "ERROR",
                Name = "ACUERDO DE MEDIOS ELECTRÓNICOS NO ACEPTADO",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "34",
                ErrorType = "ERROR",
                Name = "COMPROBANTE NO AUTORIZADO",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "35",
                ErrorType = "ERROR",
                Name = "DOCUMENTO INVÁLIDO",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "36",
                ErrorType = "ERROR",
                Name = "VERSIÓN ESQUEMA DESCONTINUADA",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "37",
                ErrorType = "ERROR",
                Name = "RUC SIN AUTORIZACIÓN DE EMISIÓN",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "39",
                ErrorType = "ERROR",
                Name = "FIRMA INVÁLIDA",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "40",
                ErrorType = "ERROR",
                Name = "ERROR EN EL CERTIFICADO",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "42",
                ErrorType = "ERROR",
                Name = "CERTIFICADO REVOCADO",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "43",
                ErrorType = "ERROR",
                Name = "CLAVE ACCESO REGISTRADA",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "45",
                ErrorType = "ERROR",
                Name = "SECUENCIAL REGISTRADO",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "46",
                ErrorType = "ERROR",
                Name = "RUC NO EXISTE",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "47",
                ErrorType = "ERROR",
                Name = "TIPO DE COMPROBANTE NO EXISTE",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "48",
                ErrorType = "ERROR",
                Name = "ESQUEMA XSD NO EXISTE",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "49",
                ErrorType = "ERROR",
                Name = "ARGUMENTOS QUE ENVÍAN AL WS NULOS",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "50",
                ErrorType = "ERROR",
                Name = "ERROR INTERNO GENERAL",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "52",
                ErrorType = "ERROR",
                Name = "ERROR EN DIFERENCIAS",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "56",
                ErrorType = "ERROR",
                Name = "ESTABLECIMIENTO CERRADO",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "57",
                ErrorType = "ERROR",
                Name = "AUTORIZACIÓN SUSPENDIDA",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "58",
                ErrorType = "ERROR",
                Name = "ERROR EN LA ESTRUCTURA DE CLAVE ACCESO",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "59",
                ErrorType = "WARNING",
                Name = "IDENTIFICACIÓN NO EXISTE",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "60",
                ErrorType = "WARNING",
                Name = "AMBIENTE EJECUCIÓN",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "62",
                ErrorType = "WARNING",
                Name = "IDENTIFICACIÓN INCORRECTA",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "63",
                ErrorType = "ERROR",
                Name = "RUC CLAUSURADO",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "64",
                ErrorType = "ERROR",
                Name = "CÓDIGO DOCUMENTO SUSTENTO",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "65",
                ErrorType = "ERROR",
                Name = "FECHA DE EMISIÓN EXTEMPORÁNEA",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "67",
                ErrorType = "ERROR",
                Name = "FECHA INVÁLIDA",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "68",
                ErrorType = "WARNING",
                Name = "DOCUMENTO SUSTENTO",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "69",
                ErrorType = "ERROR",
                Name = "IDENTIFICACIÓN DEL RECEPTOR",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "70",
                ErrorType = "ERROR",
                Name = "CLAVE DE ACCESO EN PROCESAMIENTO",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "80",
                ErrorType = "ERROR",
                Name = "ERROR EN LA ESTRUCTURA DE CLAVE ACCESO",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "82",
                ErrorType = "ERROR",
                Name = "ERROR EN LA FECHA DE INICIO DE TRANSPORTE",
                IsActive = true,
            },
            new SriErrorCode
            {
                Code = "92",
                ErrorType = "ERROR",
                Name = "ERROR AL VALIDAR MONTO DE DEVOLUCIÓN DEL IVA",
                IsActive = true,
            }
        );
    }
}
