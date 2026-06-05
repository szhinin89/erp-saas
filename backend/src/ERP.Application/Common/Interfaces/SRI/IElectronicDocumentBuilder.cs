using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Common.Interfaces.SRI;

/// <summary>
/// Construye el XML de comprobantes electrónicos SRI Ecuador.
/// Abstrae SriXmlInvoiceBuilder (static) para permitir pruebas unitarias y
/// futuras implementaciones alternativas (v2 XSD, mock en tests).
/// </summary>
public interface IElectronicDocumentBuilder
{
    /// <summary>Genera el XML de una salesBill de venta (tipo 01).</summary>
    string BuildFactura(SalesBill salesBill, List<SalesBillLine> lineas, SriSettings cfg, Company company);

    /// <summary>Genera el XML de una note de crédito o débito de venta.</summary>
    string BuildNotaCreditoDebito(SalesBill facturaOrigen, SalesNote note, List<SalesNoteLine> lineas, SriSettings cfg, Company company);
}
