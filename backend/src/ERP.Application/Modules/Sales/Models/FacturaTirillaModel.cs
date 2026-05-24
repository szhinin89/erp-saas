using ERP.Domain.Configuration.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Sales.Models;

public sealed class FacturaTirillaModel
{
    public SalesBill Venta { get; set; } = null!;
    public BusinessPartner? Buyer { get; set; }
    public BillingSettings Configuracion { get; set; } = null!;
    public bool EsPrueba { get; set; }
}
