using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// Convención por defecto: <c>electronic-documents/{tenantId:N}/{documentType}/{electronicDocumentId:N}/{draft|signed}.xml</c>.
/// Mismo estilo de segmentación que <c>MediaService</c> (tenant primero, luego el propietario
/// lógico del archivo) — no inventa una convención nueva.
/// </summary>
public sealed class ElectronicDocumentStorageNamingStrategy
    : IElectronicDocumentStorageNamingStrategy
{
    public string BuildRelativePath(
        Guid tenantId,
        ElectronicDocumentType documentType,
        Guid electronicDocumentId,
        ElectronicDocumentXmlVariant variant
    )
    {
        return string.Join(
            '/',
            "electronic-documents",
            tenantId.ToString("N"),
            documentType.ToString().ToLowerInvariant(),
            electronicDocumentId.ToString("N"),
            FileName(variant)
        );
    }

    /// <summary><c>…/{electronicDocumentId:N}/attempts/{attemptId:N}/{draft|signed}.xml</c>.</summary>
    public string BuildAttemptRelativePath(
        Guid tenantId,
        ElectronicDocumentType documentType,
        Guid electronicDocumentId,
        Guid attemptId,
        ElectronicDocumentXmlVariant variant
    )
    {
        if (variant == ElectronicDocumentXmlVariant.Authorized)
            throw new ArgumentOutOfRangeException(
                nameof(variant),
                variant,
                "El XML autorizado no se versiona por intento."
            );

        return string.Join(
            '/',
            "electronic-documents",
            tenantId.ToString("N"),
            documentType.ToString().ToLowerInvariant(),
            electronicDocumentId.ToString("N"),
            "attempts",
            attemptId.ToString("N"),
            FileName(variant)
        );
    }

    private static string FileName(ElectronicDocumentXmlVariant variant) =>
        variant switch
        {
            ElectronicDocumentXmlVariant.Draft => "draft.xml",
            ElectronicDocumentXmlVariant.Signed => "signed.xml",
            ElectronicDocumentXmlVariant.Authorized => "authorized.xml",
            _ => throw new ArgumentOutOfRangeException(
                nameof(variant),
                variant,
                "Variante de XML no soportada."
            ),
        };
}
