namespace ERP.Domain.Modules.SriCatalogs.Constants;

/// <summary>
/// RETENTIONS-ELECTRONIC-DOCUMENT-MODEL-03A — códigos técnicos SRI de tipo de impuesto retenido
/// (nodo <c>&lt;impuesto&gt;/&lt;codigo&gt;</c> del comprobante de retención, Ficha Técnica SRI
/// Tabla 21), estructuralmente fijos por el protocolo — no catálogo editable, mismo criterio que
/// <see cref="SriDocumentTypeCodes"/>. Distinto de <c>ERP.Domain.Modules.Purchases.SriTaxCategoryCodes</c>
/// (esa clase resuelve <c>&lt;impuesto&gt;/&lt;codigo&gt;</c> de Factura/Nota de Crédito — Tabla
/// 16, VAT/ICE/IRBPNR — una tabla normativa distinta con sus propios códigos, "1"=Renta no existe
/// ahí). Se mapea desde <see cref="ERP.Domain.Modules.Retentions.Enums.RetentionTaxType"/> — nunca
/// un literal suelto en el proveedor de datos.
/// </summary>
public static class SriRetentionTaxTypeCodes
{
    public const string Income = "1";
    public const string Vat = "2";
}
