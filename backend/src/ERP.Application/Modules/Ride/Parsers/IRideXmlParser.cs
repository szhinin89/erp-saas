using ERP.Application.Common;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Parsers;

/// <summary>
/// Strategy (ADR-025 §9): un implementador por <see cref="RideDocumentType"/>. Recibe
/// exclusivamente el XML autorizado (string) — nunca vuelve a consultar ElectronicDocuments,
/// Sales ni ningún otro módulo. Un nuevo tipo de comprobante se incorpora agregando un nuevo
/// implementador y registrándolo en DI (ADR-025 §10) — nunca modificando este contrato.
/// </summary>
public interface IRideXmlParser
{
    RideDocumentType DocumentType { get; }

    Result<RideModel> Parse(string authorizedXml);
}
