using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RazorLight;
using ERP.Application.Common.Interfaces;
using ERP.Application.Sales.Models;
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

    public async Task<string> GenerarHtmlFacturaAsync(Guid     salesBillId, CancellationToken ct = default)
    {
        var venta = await _dbContext.Set<ERP.Domain.Modules.Sales.Entities.SalesBill>()
            .Include(v => v.Lines)
            .FirstOrDefaultAsync(v => v.Id == salesBillId && v.Status == "Autorizado", ct);

        if (venta is null)
            throw new KeyNotFoundException("Factura no encontrada o no autorizada.");

        var config = await _dbContext.Set<BillingSettings>()
            .FirstOrDefaultAsync(c => c.SubscriberId == venta.SubscriberId, ct);

        if (config is null)
        {
            _logger.LogWarning("No existe configuración de facturación para el tenant {SubscriberId}. Usando valores por defecto.", venta.SubscriberId);
            config = BillingSettings.CreateDefault(venta.SubscriberId, Guid.Empty);
        }

        var buyer = await _dbContext.Set<ERP.Domain.MasterData.Entities.BusinessPartner>()
            .FirstOrDefaultAsync(b => b.Id == venta.BusinessPartnerId, ct);

        var model = new FacturaTirillaModel
        {
            Venta = venta,
            Buyer = buyer,
            Configuracion = config,
            EsPrueba = string.IsNullOrWhiteSpace(venta.AccessKey) || !venta.AccessKey.StartsWith("1")
        };

        var html = await _razorEngine.CompileRenderAsync(TemplateFileName, model);
        return html;
    }
}
