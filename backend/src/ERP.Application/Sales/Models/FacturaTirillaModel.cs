using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Sales.Models;

public sealed class FacturaTirillaModel
{
    public VentasFactura Venta { get; set; } = null!;
    public ConfiguracionFacturacion Configuracion { get; set; } = null!;
    public bool EsPrueba { get; set; }
}
