using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RazorLight;
using ERP.Application.Common.Interfaces;
using ERP.Application.Ventas.Models;
using ERP.Domain.Configuration.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Services;

public sealed class TirillaFacturaService : ITirillaFacturaService
{
    private readonly ErpDbContext _dbContext;
    private readonly IRazorLightEngine _razorEngine;
    private readonly ILogger<TirillaFacturaService> _logger;
    private const string TemplateFileName = "TirillaFactura.cshtml";

    public TirillaFacturaService(
        ErpDbContext dbContext,
        IWebHostEnvironment env,
        ILogger<TirillaFacturaService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;

        var templatesDir = Path.Combine(env.ContentRootPath, "Templates");
        if (!Directory.Exists(templatesDir))
            throw new DirectoryNotFoundException($"El directorio de plantillas no existe: {templatesDir}");

        _razorEngine = new RazorLightEngineBuilder()
            .UseFileSystemProject(templatesDir)
            .UseMemoryCachingProvider()
            .Build();
    }

    public async Task<string> GenerarHtmlFacturaAsync(Guid ventaId, CancellationToken ct = default)
    {
        var venta = await _dbContext.Set<ERP.Domain.Ventas.Entities.VentasFactura>()
            .Include(v => v.Cliente)
            .Include(v => v.Detalles)
            .FirstOrDefaultAsync(v => v.Id == ventaId && v.Estado == "Autorizado", ct);

        if (venta is null)
            throw new KeyNotFoundException("Factura no encontrada o no autorizada.");

        var config = await _dbContext.Set<ConfiguracionFacturacion>()
            .FirstOrDefaultAsync(c => c.TenantId == venta.TenantId, ct);

        if (config is null)
        {
            _logger.LogWarning("No existe configuración de facturación para el tenant {TenantId}. Usando valores por defecto.", venta.TenantId);
            config = ConfiguracionFacturacion.CreateDefault(venta.TenantId, Guid.Empty);
        }

        var model = new FacturaTirillaModel
        {
            Venta = venta,
            Configuracion = config,
            EsPrueba = string.IsNullOrWhiteSpace(venta.ClaveAcceso) || !venta.ClaveAcceso.StartsWith("1")
        };

        var html = await _razorEngine.CompileRenderAsync(TemplateFileName, model);
        return html;
    }
}
