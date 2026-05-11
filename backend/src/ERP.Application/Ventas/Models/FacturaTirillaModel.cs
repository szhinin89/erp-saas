using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Ventas.Entities;

namespace ERP.Application.Ventas.Models;

public sealed class FacturaTirillaModel
{
    public VentasFactura Venta { get; set; } = null!;
    public ConfiguracionFacturacion Configuracion { get; set; } = null!;
    public bool EsPrueba { get; set; }
}
