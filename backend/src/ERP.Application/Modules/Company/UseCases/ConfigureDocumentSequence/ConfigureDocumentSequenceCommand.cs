using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.ConfigureDocumentSequence;

/// <summary>
/// DOCUMENT-SEQUENCES-CONFIG-03 — configura el próximo secuencial (<c>NextNumber</c>) de la
/// secuencia documental SRI identificada por <c>(TenantId, CompanyId, EmissionPointId,
/// DocTypeCode)</c>, antes de que entregue su primer número real. Uso previsto: empresas que
/// migran desde otro sistema y ya tienen numeración SRI en curso (ver
/// docs/decisions/DOCUMENT-SEQUENCES-DESIGN-02.md § E).
///
/// No captura ningún número — <see cref="ERP.Domain.Modules.Company.Interfaces.IDocumentSequenceRepository.CaptureNextAsync"/>
/// sigue siendo el único punto de entrada para eso. No crea ningún documento.
/// </summary>
public sealed record ConfigureDocumentSequenceCommand(
    Guid EmissionPointId,
    string DocTypeCode,
    int NextNumber
) : IRequest<Result<DocumentSequenceDto>>, ICompanyScopedRequest;
