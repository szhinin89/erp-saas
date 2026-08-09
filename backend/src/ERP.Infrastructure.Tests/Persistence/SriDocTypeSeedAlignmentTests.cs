using ERP.Domain.Modules.SriCatalogs.Constants;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Infrastructure.Persistence.Configurations.SriCatalogs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// CLEAN-01C: <c>SriDocumentTypeCodes</c> es un alias técnico para literales usados en lógica
/// interna (defaults, secuencias) — nunca debe convertirse en un catálogo paralelo que pueda
/// divergir de la fuente fiscal real, <see cref="SriDocType"/> (seed EF en
/// <see cref="SriDocTypeConfiguration"/>). Este test lee el seed directamente desde
/// <c>SriDocTypeConfiguration.Configure</c> aplicado a un <see cref="ModelBuilder"/> aislado (sin
/// conexión real a base de datos) y falla si alguno de los códigos nombrados deja de existir
/// activo/electrónico en el catálogo persistido — por ejemplo, si alguna vez se desactiva/retira
/// "01", "04" o "07" del seed sin actualizar la constante en el mismo cambio.
/// </summary>
public sealed class SriDocTypeSeedAlignmentTests
{
    [Theory]
    [InlineData(nameof(SriDocumentTypeCodes.Invoice), SriDocumentTypeCodes.Invoice)]
    [InlineData(nameof(SriDocumentTypeCodes.CreditNote), SriDocumentTypeCodes.CreditNote)]
    [InlineData(nameof(SriDocumentTypeCodes.Withholding), SriDocumentTypeCodes.Withholding)]
    public void Every_SriDocumentTypeCodes_constant_exists_active_in_the_SriDocType_seed(
        string constantName,
        string code
    )
    {
        _ = constantName;
        var seededCodes = GetSeededActiveElectronicDocTypeCodes();

        seededCodes
            .Should()
            .Contain(
                code,
                $"SriDocumentTypeCodes.{constantName} debe seguir existiendo activo y electrónico en el seed de SriDocType"
            );
    }

    private static HashSet<string> GetSeededActiveElectronicDocTypeCodes()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.ApplyConfiguration(new SriDocTypeConfiguration());

        var entityType = modelBuilder.Model.FindEntityType(typeof(SriDocType))!;
        return entityType
            .GetSeedData()
            .Where(seed =>
                (bool)seed[nameof(SriDocType.IsActive)]!
                && (bool)seed[nameof(SriDocType.IsElectronic)]!
            )
            .Select(seed => (string)seed[nameof(SriDocType.Code)]!)
            .ToHashSet();
    }
}
