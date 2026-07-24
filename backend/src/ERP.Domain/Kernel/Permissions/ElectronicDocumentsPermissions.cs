namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// Permisos del Monitor de Documentos Electrónicos sobre <c>ElectronicDocument</c> (estado,
/// timeline, XML, reintento manual). Distintos de <see cref="ElectronicInvoicingPermissions"/>,
/// que gobiernan la configuración SRI.
/// </summary>
public static class ElectronicDocumentsPermissions
{
    public const string View = "electronic-documents.view";
    public const string Detail = "electronic-documents.detail";

    /// <summary>Reintentar manualmente un documento varado (Signed/Received/DeadLetter). Única acción de escritura del Monitor.</summary>
    public const string Retry = "electronic-documents.retry";
}
