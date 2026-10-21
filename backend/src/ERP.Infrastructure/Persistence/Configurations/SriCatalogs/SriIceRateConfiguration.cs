using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Src = ERP.Infrastructure.Persistence.Configurations.SriCatalogs.SriNormativeSourceConfiguration;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

/// <summary>
/// Catálogo ICE GLOBAL. Definición vigente: docs/architecture/architecture.md § ICE: catálogo oficial.
/// Códigos y conceptos: Ficha técnica offline 2.34, Tabla 18. Tarifas: normativa de cada fila (NormativeSourceId).
/// IsActive es disponibilidad operativa, no validez normativa.
/// </summary>
public class SriIceRateConfiguration : IEntityTypeConfiguration<SriIceRate>
{
    private const string PerCigarette = "USD por unidad de cigarrillo";
    private const string PerPureAlcoholLiter = "USD por litro de alcohol puro";
    private const string PerSugar = "USD por 100 gramos de azúcar añadida";
    private const string PerPlasticBag = "USD por funda plástica";
    private static readonly DateOnly Fiscal2026From = new(2026, 1, 1);
    private static readonly DateOnly Fiscal2026Until = new(2026, 12, 31);
    private static readonly DateOnly RestoredFrom = new(2024, 5, 21);

    public void Configure(EntityTypeBuilder<SriIceRate> builder)
    {
        builder.ToTable("sri_ice_rate", "global", table =>
        {
            table.HasCheckConstraint(
                "ck_sri_ice_mixed_catalog_only",
                "calculation_type <> 3 OR (percentage IS NOT NULL AND unit_value IS NOT NULL "
                    + "AND specific_unit IS NOT NULL AND length(trim(specific_unit)) > 0 "
                    + "AND normative_source_id IS NOT NULL AND is_active = false)");
            table.HasCheckConstraint(
                "ck_sri_ice_rate_validity",
                "valid_from IS NULL OR valid_until IS NULL OR valid_from <= valid_until");
        });
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(10);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Percentage).HasColumnName("percentage").HasPrecision(8, 4);
        builder.Property(x => x.UnitValue).HasColumnName("unit_value").HasPrecision(10, 4);
        builder.Property(x => x.SpecificUnit).HasColumnName("specific_unit").HasMaxLength(100);
        builder.Property(x => x.NormativeSourceId).HasColumnName("normative_source_id");
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidUntil).HasColumnName("valid_until");
        builder.HasOne<SriNormativeSource>().WithMany().HasForeignKey(x => x.NormativeSourceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .Property(x => x.CalculationType)
            .HasColumnName("calculation_type")
            .HasConversion<int>()
            .HasDefaultValue(SriTaxCalculationType.Percentage)
            .HasSentinel((SriTaxCalculationType)0)
            .IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).ValueGeneratedNever();

        builder.HasData(
            // Grupo IV — mixta (Res. NAC-DGERCGC25-00000040, período 2026; ad valorem 75% LRTI art. 82).
            // Sin cálculo operativo mixto: ck_sri_ice_mixed_catalog_only exige is_active = false.
            Mixed("3031", "ICE Bebidas Alcohólicas", 10.41m),
            Mixed("3033", "ICE Alcohol", 10.41m),
            Mixed("3041", "ICE Cerveza Industrial", 13.62m),
            Mixed("3043", "ICE Cerveza Artesanal", 1.56m),
            Mixed("3532", "ICE Import. alcohol SENAE", 10.41m),
            Mixed("3533", "ICE Import. Bebidas Alcohólicas", 10.41m),
            Mixed("3541", "ICE Cerveza Industrial CAE", 13.62m),
            Mixed("3545", "ICE cerveza artesanal SENAE", 1.56m),

            // Específicas 2026 con base física distinta de la unidad transferida. Ventas y las líneas
            // manuales multiplican UnitValue por la cantidad en UoM base, así que las filas activas
            // conservan UnitValue nulo: Compras usa el monto exacto del XML del proveedor. Las
            // filas nuevas entran deshabilitadas con sus componentes completos.
            Specific("3011", "ICE Cigarrillos Rubios", PerCigarette, null, true),
            Specific("3021", "ICE Cigarrillos Negros", PerCigarette, null, true),
            Specific("3053", "ICE Bebidas Gaseosas con Alto Contenido de Azúcar", PerSugar, null, true),
            Specific("3111", "ICE Bebidas No Alcohólicas", PerSugar, null, true),
            Specific("3542", "ICE Cigarrillos Rubios CAE", PerCigarette, 0.16m, false),
            Specific("3543", "ICE Cigarrillos Negros CAE", PerCigarette, 0.16m, false),
            Specific("3552", "ICE bebidas gaseosas con alto contenido de azúcar SENAE", PerSugar, 0.18m, false),
            Specific("3602", "ICE bebidas no alcohólicas SENAE", PerSugar, 0.18m, false),
            // Anexo 18: el mismo código cubre fundas normales, con rebaja del 50% y exentas; una
            // tarifa única no calcula las tres variantes. Habilitación pendiente de decisión.
            Specific("3680", "ICE fundas plásticas", PerPlasticBag, 0.08m, false),

            // Grupos I, II y III — ad valorem según LRTI art. 82 (codificación SRI, reforma 25-III-2024).
            AdValorem("3023", "ICE Productos del Tabaco y Sucedáneos del Tabaco excepto Cigarrillos", 150m),
            AdValorem("3544", "ICE Productos del Tabaco y Sucedáneos del Tabaco Excepto Cigarrillos CAE", 150m),
            AdValorem("3054", "ICE Bebidas Gaseosas con Bajo Contenido de Azúcar", 10m),
            AdValorem("3553", "ICE bebidas gaseosas con bajo contenido de azúcar SENAE", 10m),
            AdValorem("3101", "ICE Bebidas Energizantes", 10m),
            AdValorem("3601", "ICE Bebidas Energizantes SENAE", 10m),
            AdValorem("3610", "ICE Perfumes y Aguas de Tocador", 20m),
            AdValorem("3710", "ICE Perfumes Aguas de Tocador Cae", 20m),
            AdValorem("3640", "ICE Focos Incandescentes", 100m),
            AdValorem("3740", "ICE Focos Incandescentes CAE", 100m),
            AdValorem("3073", "ICE Vehículos Motorizados cuyo PVP sea hasta de 20000 USD", 5m),
            AdValorem("3871", "ICE-vehículos motorizados cuyo PVP SEA hasta de 20000 USD SENAE", 5m),
            AdValorem("3684", "ICE vehículos motorizados camionetas y de rescate cuyo PVP sea hasta DE 30.000 USD", 5m),
            AdValorem("3685", "ICE vehículos motorizados camionetas y de rescate PVP sea hasta DE 30.000 USD SENAE", 5m),
            AdValorem("3686", "ICE vehículos motorizados excepto camionetas y de rescate cuyo PVP sea superior USD 20.000 hasta DE 30.000", 10m),
            AdValorem("3687", "ICE vehículos motorizados excepto camionetas y de rescate cuyo PVP sea superior USD 20.000 hasta de 30.000 SENAE", 10m),
            AdValorem("3075", "ICE Vehículos Motorizados PVP entre 30000 y 40000", 15m),
            AdValorem("3873", "ICE-vehículos motorizados PVP entre 30000 Y 40000 SENAE", 15m),
            AdValorem("3077", "ICE Vehículos Motorizados cuyo PVP superior USD 40.000 hasta 50.000", 20m),
            AdValorem("3874", "ICE-vehículos motorizados cuyo PVP superior USD 40.000 hasta 50.000 SENAE", 20m),
            AdValorem("3078", "ICE Vehículos Motorizados cuyo PVP superior USD 50.000 hasta 60.000", 25m),
            AdValorem("3875", "ICE-vehículos motorizados cuyo PVP superior USD 50.000 hasta 60.000 SENAE", 25m),
            AdValorem("3079", "ICE Vehículos Motorizados cuyo PVP superior USD 60.000 hasta 70.000", 30m),
            AdValorem("3876", "ICE-vehículos motorizados cuyo PVP superior USD 60.000 hasta 70.000 SENAE", 30m),
            AdValorem("3080", "ICE Vehículos Motorizados cuyo PVP superior USD 70.000", 35m),
            AdValorem("3877", "ICE-vehículos motorizados cuyo PVP superior USD 70.000 SENAE", 35m),
            // Tabla 18 publica 0%; el art. 82 grupo III vigente grava la televisión pagada con 15%.
            AdValorem("3092", "ICE Servicios de Televisión Prepagada", 15m),
            // Tabla 18 publica 35%; el art. 82 grupo III vigente fija 30%.
            AdValorem("3660", "ICE Cuotas Membresías Afiliaciones Acciones", 30m),

            // Tarifas restablecidas desde 2024-05-21 por la nulidad del D.E. 645 (Oficio SRI-NAC-DNJ-2024-0042-OF).
            Restored("3682", "ICE consumibles tabaco calentado y líquidos con nicotina SRI", 150m),
            Restored("3683", "ICE consumibles tabaco calentado y líquidos con nicotina SENAE", 150m),
            Restored("3081", "ICE Aviones, Tricars, yates, Barcos de Recreo", 15m),
            Restored("3878", "ICE-Aviones, Tricars, Yates, Barcos de Rec SENAE", 15m),
            Restored("3581", "ICE Aeronaves CAE", 15m),
            Restored("3582", "ICE Aviones, Avionetas y Helicópteros Exct. Aquellos destinados Al Trans. CAE", 15m),

            // Armas: 300% desde 2024-05-21; el D.E. 302 (2024-06-12) reduciría a 30% desde 2024-07-01,
            // pero su texto oficial y alcance no se verificaron. Se conserva la fila corregida de 3630
            // sin inferir tarifa y ambos códigos quedan no seleccionables.
            new SriIceRate
            {
                Code = "3630", Name = "ICE Armas de Fuego, Armas deportivas y Municiones", Percentage = 30.00m,
                CalculationType = SriTaxCalculationType.Percentage, NormativeSourceId = Src.IceRestored20240521Id,
                IsActive = false,
            },
            NotApplicable("3730", "ICE Importaciones Armas de Fuego, Armas deportivas y Municiones CAE", Src.IceRestored20240521Id),

            // Códigos de Tabla 18 sin gravamen vigente según el art. 82 y la matriz ICE del SRI.
            NotApplicable("3620", "ICE Videojuegos", Src.IceSriSummaryMatrixId),
            NotApplicable("3720", "ICE Video Juegos CAE", Src.IceSriSummaryMatrixId),
            NotApplicable("3671", "ICE calefones y sistemas de calentamiento de agua a gas SRI", Src.IceSriSummaryMatrixId),
            NotApplicable("3771", "ICE calefones y sistemas de calentamiento de agua a gas SENAE", Src.IceSriSummaryMatrixId),
            NotApplicable("3093", "ICE Servicios Telefonía Sociedades", Src.IceSriSummaryMatrixId),
            NotApplicable("3681", "ICE servicios de telefonía móvil personas naturales", Src.IceSriSummaryMatrixId),
            NotApplicable("3688", "ICE vehículos híbridos cuyo PVP sea de hasta USD. 35.000", Src.IceSriSummaryMatrixId),
            NotApplicable("3689", "ICE vehículos híbridos cuyo PVP sea de hasta USD. 35.000 SENAE", Src.IceSriSummaryMatrixId),
            NotApplicable("3690", "ICE vehículos híbridos cuyo PVP superior USD. 35.000 hasta 40.000 SENAE", Src.IceSriSummaryMatrixId),
            NotApplicable("3691", "ICE vehículos híbridos cuyo PVP superior USD. 35.000 hasta 40.000", Src.IceSriSummaryMatrixId),
            NotApplicable("3692", "ICE vehículos híbridos cuyo PVP superior USD. 40.000 hasta 50.000", Src.IceSriSummaryMatrixId),
            NotApplicable("3693", "ICE vehículos híbridos cuyo PVP superior USD. 40.000 hasta 50.000 SENAE", Src.IceSriSummaryMatrixId),
            NotApplicable("3694", "ICE vehículos híbridos cuyo PVP superior USD. 50.000 hasta 60.000 SENAE", Src.IceSriSummaryMatrixId),
            NotApplicable("3695", "ICE vehículos híbridos cuyo PVP superior USD. 50.000 hasta 60.000", Src.IceSriSummaryMatrixId),
            NotApplicable("3696", "ICE vehículos híbridos cuyo PVP superior USD. 60.000 hasta 70.000", Src.IceSriSummaryMatrixId),
            NotApplicable("3697", "ICE vehículos híbridos cuyo PVP superior USD. 60.000 hasta 70.000 SENAE", Src.IceSriSummaryMatrixId),
            NotApplicable("3698", "ICE vehículos híbridos cuyo PVP superior a USD 70.000", Src.IceSriSummaryMatrixId),
            NotApplicable("3699", "ICE vehículos híbridos cuyo PVP superior a USD 70.000 SENAE", Src.IceSriSummaryMatrixId),

            // Claves históricas ausentes de Tabla 18: se conservan con sus valores para referencias
            // anteriores y quedan no seleccionables. No se infiere un código oficial equivalente.
            Legacy("3051", "Bebidas energizantes", 10m),
            Legacy("3071", "Perfumes y aguas de tocador", 20m),
            Legacy("3072", "Videojuegos", 35m),
            Legacy("3082", "Vehículos ≤3.5t (USD 30k–40k)", 10m),
            Legacy("3083", "Vehículos ≤3.5t (más de USD 40k)", 15m),
            Legacy("3091", "Aviones / helicópteros de uso privado", 15m)
        );
    }

    private static SriIceRate Mixed(string code, string name, decimal unitValue) => new()
    {
        Code = code, Name = name, Percentage = 75m, UnitValue = unitValue, SpecificUnit = PerPureAlcoholLiter,
        CalculationType = SriTaxCalculationType.Mixed, NormativeSourceId = Src.IceSpecific2026Id,
        ValidFrom = Fiscal2026From, ValidUntil = Fiscal2026Until, IsActive = false,
    };

    private static SriIceRate Specific(string code, string name, string unit, decimal? unitValue, bool isActive) => new()
    {
        Code = code, Name = name, UnitValue = unitValue, SpecificUnit = unit,
        CalculationType = SriTaxCalculationType.Specific, NormativeSourceId = Src.IceSpecific2026Id,
        ValidFrom = Fiscal2026From, ValidUntil = Fiscal2026Until, IsActive = isActive,
    };

    private static SriIceRate AdValorem(string code, string name, decimal percentage) => new()
    {
        Code = code, Name = name, Percentage = percentage, CalculationType = SriTaxCalculationType.Percentage,
        NormativeSourceId = Src.LrtiArticle82Id,
    };

    private static SriIceRate Restored(string code, string name, decimal percentage) => new()
    {
        Code = code, Name = name, Percentage = percentage, CalculationType = SriTaxCalculationType.Percentage,
        NormativeSourceId = Src.IceRestored20240521Id, ValidFrom = RestoredFrom,
    };

    private static SriIceRate NotApplicable(string code, string name, Guid sourceId) => new()
    {
        Code = code, Name = name, CalculationType = SriTaxCalculationType.Percentage,
        NormativeSourceId = sourceId, IsActive = false,
    };

    private static SriIceRate Legacy(string code, string name, decimal percentage) => new()
    {
        Code = code, Name = name, Percentage = percentage, CalculationType = SriTaxCalculationType.Percentage,
        NormativeSourceId = Src.FichaV234Table18Id, IsActive = false,
    };
}
