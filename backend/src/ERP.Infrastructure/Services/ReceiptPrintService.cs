using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RazorLight;
using ERP.Application.Common.Interfaces;
using ERP.Application.Sales.Models;
using ERP.Domain.Configuration.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Services;

public sealed class ReceiptPrintService : IReceiptPrintService
{
    private readonly ErpDbContext _dbContext;
    private readonly IRazorLightEngine _razorEngine;
    private readonly ILogger<ReceiptPrintService> _logger;
    private const string TemplateFileName = "TirillaFactura.cshtml";

    public ReceiptPrintService(
        ErpDbContext dbContext,
        IWebHostEnvironment env,
        ILogger<ReceiptPrintService> logger)
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

    public async Task<string> GenerateInvoiceHtmlAsync(Guid salesBillId, CancellationToken ct = default)
    {
        var venta = await _dbContext.FiscalInvoices
            .Include(v => v.Lines)
            .Include(v => v.Electronic)
            .FirstOrDefaultAsync(v => v.PublicId == salesBillId && v.Status == "Autorizado", ct);

        if (venta is null)
            throw new KeyNotFoundException("salesBill no encontrada o no autorizada.");

        var config = await _dbContext.Set<SubscriberBillingProfile>()
            .FirstOrDefaultAsync(c => c.SubscriberId == venta.SubscriberId, ct);

        if (config is null)
        {
            _logger.LogWarning("No existe configuraciÃ³n de facturaciÃ³n para el tenant {SubscriberId}. Usando valores por defecto.", venta.SubscriberId);
            config = SubscriberBillingProfile.CreateDefault(venta.SubscriberId, Guid.Empty);
        }

        var buyer = await _dbContext.Set<ERP.Domain.MasterData.Entities.BusinessPartner>()
            .FirstOrDefaultAsync(b => b.Id == venta.BusinessPartnerId, ct);

        var model = new ReceiptModel
        {
            Invoice = ReceiptDocument.FromInvoice(venta),
            Buyer = buyer,
            Configuration = config,
            IsTestMode = string.IsNullOrWhiteSpace(venta.AccessKey) || !venta.AccessKey.StartsWith('1')
        };

        return await _razorEngine.CompileRenderAsync(TemplateFileName, model);
    }
}
