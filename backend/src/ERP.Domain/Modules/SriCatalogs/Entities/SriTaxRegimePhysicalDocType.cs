namespace ERP.Domain.Modules.SriCatalogs.Entities;

/// <summary>
/// PHYSICAL-FISCAL-F4/F5 — tipos de comprobante que un régimen tributario puede emitir en forma
/// física (preimpresa). Separa "permitido en físico para el régimen" de
/// <see cref="SriDocType.IsElectronic"/> ("apto para emisión electrónica"). Sin fila activa, el
/// régimen no puede emitir ese tipo en físico (fail-closed). <see cref="LegalBasis"/> registra la
/// norma de la que sale cada fila.
/// </summary>
public class SriTaxRegimePhysicalDocType
{
    public string TaxRegimeCode { get; set; } = null!;
    public string DocTypeCode { get; set; } = null!;
    public string LegalBasis { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
