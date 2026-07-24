using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// Convención por defecto: <c>electronic-documents/{tenantId:N}/{documentType}/{electronicDocumentId:N}/{draft|signed}.xml</c>.
/// Mismo estilo de segmentación que <c>MediaService</c> (tenant primero, luego el propietario
/// lógico del archivo) — no inventa una convención nueva.
/// </summary>
public sealed class ElectronicDocumentStorageNamingStrategy : IElectronicDocumentStorageNamingStrategy
{
    public string BuildRelativePath(
        Guid tenantId, ElectronicDocumentType documentType, Guid electronicDocumentId, ElectronicDocumentXmlVariant variant)
    {
        var fileName = variant switch
        {
            ElectronicDocumentXmlVariant.Draft => "draft.xml",
            ElectronicDocumentXmlVariant.Signed => "signed.xml",
            ElectronicDocumentXmlVariant.Authorized => "authorized.xml",
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Variante de XML no soportada."),
        };

        return string.Join('/',
            "electronic-documents",
            tenantId.ToString("N"),
            documentType.ToString().ToLowerInvariant(),
            electronicDocumentId.ToString("N"),
            fileName);
    }
}
