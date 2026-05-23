using ERP.Application.Common.Interfaces.SRI;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Infrastructure.Services.Sri;

/// <summary>
/// Adapta SriXmlFacturaBuilder (estático) a la interfaz inyectable IElectronicDocumentBuilder.
/// Registrado como Singleton ya que no tiene estado.
/// </summary>
public sealed class ElectronicDocumentBuilderAdapter : IElectronicDocumentBuilder
{
    public string BuildFactura(SalesBill factura, List<SalesBillLine> lineas, SriSettings cfg)
        => SriXmlFacturaBuilder.BuildFactura(factura, lineas, cfg);

    public string BuildNotaCreditoDebito(SalesBill facturaOrigen, SalesNote nota, List<SalesNoteLine> lineas, SriSettings cfg)
        => SriXmlFacturaBuilder.BuildNotaCreditoDebito(facturaOrigen, nota, lineas, cfg);
}
