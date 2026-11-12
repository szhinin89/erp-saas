namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// Permisos del Monitor de comprobantes sobre <c>ElectronicDocument</c> (estado,
/// timeline, XML, reintento manual). Distintos de <see cref="ElectronicInvoicingPermissions"/>,
/// que gobiernan la configuración SRI.
/// </summary>
public static class ElectronicDocumentsPermissions
{
    public const string View = "electronic-documents.view";
    public const string Detail = "electronic-documents.detail";

    /// <summary>Reintentar manualmente un documento varado (Signed/Received/DeadLetter).</summary>
    public const string Retry = "electronic-documents.retry";

    /// <summary>Caso #32 — registrar evidencia de conciliación manual de un comprobante con resultado SRI desconocido y reconsultarlo.</summary>
    public const string Reconcile = "electronic-documents.reconcile";

    /// <summary>
    /// R12 Fase B — corregir y retransmitir con la misma clave un comprobante rechazado por el SRI.
    /// Distinto de <see cref="Retry"/>: es la única acción que vuelve a transmitir un comprobante.
    /// </summary>
    public const string RetransmitCorrected = "electronic-documents.retransmit-corrected";
}
