namespace ERP.Domain.Modules.ElectronicDocuments.Enums;

/// <summary>Qué XML persistido de un documento electrónico se solicita.</summary>
public enum ElectronicDocumentXmlVariant
{
    Draft = 1,
    Signed = 2,

    /// <summary>XML autorizado devuelto por el SRI (Fase 9) — solo existe si el documento llegó a Authorized.</summary>
    Authorized = 3,
}
