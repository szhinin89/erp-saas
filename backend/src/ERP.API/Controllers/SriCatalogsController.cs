using ERP.Application.Modules.SriCatalogs.DTOs;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.API.Controllers;

/// <summary>
/// Catálogos regulatorios del SRI Ecuador.
/// Datos globales — no requieren tenant scope ni autenticación.
/// Cacheados en IMemoryCache (1 hora): solo cambian por reforma tributaria.
/// No pasan por la pipeline MediatR/CQRS — son lookups de referencia pura.
/// </summary>
[ApiController]
[Route("api/catalogs/sri")]
[AllowAnonymous]
[Produces("application/json")]
[Tags("SRI Catálogos")]
public sealed class SriCatalogsController : ControllerBase
{
    private readonly ErpDbContext _db;
    private readonly IMemoryCache  _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    public SriCatalogsController(ErpDbContext db, IMemoryCache cache)
    {
        _db    = db;
        _cache = cache;
    }

    private async Task<T> Cached<T>(string key, Func<Task<T>> factory) where T : class
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;
        var value = await factory();
        _cache.Set(key, value, CacheTtl);
        return value;
    }

    /// <summary>Formas de pago SRI (01, 15–21). Solo activas.</summary>
    [HttpGet("payment-methods")]
    [ProducesResponseType(typeof(IReadOnlyList<SriPaymentMethodDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentMethods(CancellationToken ct)
        => Ok(await Cached("sri:payment-methods", async () =>
            await _db.SriPaymentMethods.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Code)
                .Select(x => new SriPaymentMethodDto(x.Code, x.Name, x.IsActive))
                .ToListAsync(ct)));

    /// <summary>Tipos de comprobante SRI (01=salesBill, 04=NC, 07=Retención…).</summary>
    [HttpGet("doc-types")]
    [ProducesResponseType(typeof(IReadOnlyList<SriDocTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDocTypes(CancellationToken ct)
        => Ok(await Cached("sri:doc-types", async () =>
            await _db.SriDocTypes.AsNoTracking()
                .OrderBy(x => x.Code)
                .Select(x => new SriDocTypeDto(x.Code, x.Name, x.ShortName, x.IsElectronic, x.IsActive))
                .ToListAsync(ct)));

    /// <summary>Tipos de identificación SRI (04=RUC, 05=CI, 06=PASSPORT, 07=Consumidor Final…).</summary>
    [HttpGet("id-types")]
    [ProducesResponseType(typeof(IReadOnlyList<SriIdTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIdTypes(CancellationToken ct)
        => Ok(await Cached("sri:id-types", async () =>
            await _db.SriIdTypes.AsNoTracking()
                .OrderBy(x => x.Code)
                .Select(x => new SriIdTypeDto(x.Code, x.Name, x.Digits))
                .ToListAsync(ct)));

    /// <summary>Tarifas IVA con historial 2008–2024. Incluye activas e históricas.</summary>
    [HttpGet("vat-rates")]
    [ProducesResponseType(typeof(IReadOnlyList<SriVatRateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVatRates(CancellationToken ct)
        => Ok(await Cached("sri:vat-rates", async () =>
            await _db.SriVatRates.AsNoTracking()
                .OrderBy(x => x.Code)
                .Select(x => new SriVatRateDto(x.Code, x.Name, x.Percentage, x.IsActive, x.ValidFrom, x.ValidUntil))
                .ToListAsync(ct)));

    /// <summary>Tarifas ICE (cigarrillos, bebidas, vehículos…). Solo activas.</summary>
    [HttpGet("ice-rates")]
    [ProducesResponseType(typeof(IReadOnlyList<SriIceRateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIceRates(CancellationToken ct)
        => Ok(await Cached("sri:ice-rates", async () =>
            await _db.SriIceRates.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Code)
                .Select(x => new SriIceRateDto(x.Code, x.Name, x.Percentage, x.UnitValue, x.IsActive))
                .ToListAsync(ct)));

    /// <summary>Códigos de retención. Filtrable: ?taxType=IVA | RENTA | ISD</summary>
    [HttpGet("retention-codes")]
    [ProducesResponseType(typeof(IReadOnlyList<SriRetentionCodeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRetentionCodes(
        [FromQuery] string? taxType = null, CancellationToken ct = default)
    {
        var key = $"sri:retention:{taxType ?? "all"}";
        return Ok(await Cached(key, async () =>
        {
            var q = _db.SriRetentionCodes.AsNoTracking().Where(x => x.IsActive);
            if (!string.IsNullOrWhiteSpace(taxType))
                q = q.Where(x => x.TaxType == taxType.ToUpperInvariant());
            return await q.OrderBy(x => x.TaxType).ThenBy(x => x.Code)
                .Select(x => new SriRetentionCodeDto(x.TaxType, x.Code, x.Name, x.Percentage, x.AppliesTo, x.IsActive))
                .ToListAsync(ct);
        }));
    }

    /// <summary>Países SRI con ISO-3, ISO-2 y código telefónico. Solo activos.</summary>
    [HttpGet("countries")]
    [ProducesResponseType(typeof(IReadOnlyList<SriCountryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCountries(CancellationToken ct)
        => Ok(await Cached("sri:countries", async () =>
            await _db.SriCountries.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new SriCountryDto(x.Code, x.Iso2, x.Name, x.PhoneCode, x.IsActive))
                .ToListAsync(ct)));

    /// <summary>Regímenes tributarios (General, RIMPE, Contribuyente Especial).</summary>
    [HttpGet("tax-regimes")]
    [ProducesResponseType(typeof(IReadOnlyList<SriTaxRegimeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTaxRegimes(CancellationToken ct)
        => Ok(await Cached("sri:tax-regimes", async () =>
            await _db.SriTaxRegimes.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Code)
                .Select(x => new SriTaxRegimeDto(x.Code, x.Name, x.Abbrev, x.IsActive))
                .ToListAsync(ct)));

    /// <summary>Sustentos tributarios para retenciones y comprobantes. Solo activos.</summary>
    [HttpGet("tax-supports")]
    [ProducesResponseType(typeof(IReadOnlyList<SriTaxSupportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTaxSupports(CancellationToken ct)
        => Ok(await Cached("sri:tax-supports", async () =>
            await _db.SriTaxSupports.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Code)
                .Select(x => new SriTaxSupportDto(x.Code, x.Name, x.IsActive))
                .ToListAsync(ct)));

    /// <summary>Unidades de medida SRI para lines de salesBill. Solo activas.</summary>
    [HttpGet("uom")]
    [ProducesResponseType(typeof(IReadOnlyList<SriUomDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUom(CancellationToken ct)
        => Ok(await Cached("sri:uom", async () =>
            await _db.SriUoms.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Code)
                .Select(x => new SriUomDto(x.Code, x.Name, x.Abbrev, x.IsActive))
                .ToListAsync(ct)));

    /// <summary>Ambientes SRI (1=Producción, 2=Pruebas).</summary>
    [HttpGet("environments")]
    [ProducesResponseType(typeof(IReadOnlyList<SriEnvironmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEnvironments(CancellationToken ct)
        => Ok(await Cached("sri:environments", async () =>
            await _db.SriEnvironments.AsNoTracking()
                .OrderBy(x => x.Code)
                .Select(x => new SriEnvironmentDto(x.Code, x.Name, x.Abbrev))
                .ToListAsync(ct)));

    /// <summary>Tipos de emisión (1=Normal, 2=Indisponibilidad del sistema).</summary>
    [HttpGet("emission-types")]
    [ProducesResponseType(typeof(IReadOnlyList<SriEmissionTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmissionTypes(CancellationToken ct)
        => Ok(await Cached("sri:emission-types", async () =>
            await _db.SriEmissionTypes.AsNoTracking()
                .OrderBy(x => x.Code)
                .Select(x => new SriEmissionTypeDto(x.Code, x.Name))
                .ToListAsync(ct)));

    /// <summary>Códigos de error SRI. Filtrable: ?errorType=ERROR | WARNING | INFO</summary>
    [HttpGet("error-codes")]
    [ProducesResponseType(typeof(IReadOnlyList<SriErrorCodeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetErrorCodes(
        [FromQuery] string? errorType = null, CancellationToken ct = default)
    {
        var key = $"sri:error:{errorType ?? "all"}";
        return Ok(await Cached(key, async () =>
        {
            var q = _db.SriErrorCodes.AsNoTracking().Where(x => x.IsActive);
            if (!string.IsNullOrWhiteSpace(errorType))
                q = q.Where(x => x.ErrorType == errorType.ToUpperInvariant());
            return await q.OrderBy(x => x.Code)
                .Select(x => new SriErrorCodeDto(x.Code, x.ErrorType, x.Name, x.Description, x.IsActive))
                .ToListAsync(ct);
        }));
    }
}
