using ERP.Application.Common.Interfaces.SRI;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Infrastructure.Services.Sri;

/// <summary>
/// Adapta SriXmlInvoiceBuilder (estático) a la interfaz inyectable IElectronicDocumentBuilder.
/// Registrado como Singleton ya que no tiene estado.
/// </summary>
public sealed class ElectronicDocumentBuilderAdapter : IElectronicDocumentBuilder
{
    public string BuildFactura(SalesBill salesBill, List<SalesBillLine> lineas, SriSettings cfg, Company company)
        => SriXmlInvoiceBuilder.BuildFactura(salesBill, lineas, cfg, company);

    public string BuildNotaCreditoDebito(SalesBill facturaOrigen, SalesNote note, List<SalesNoteLine> lineas, SriSettings cfg, Company company)
        => SriXmlInvoiceBuilder.BuildNotaCreditoDebito(facturaOrigen, note, lineas, cfg, company);
}
