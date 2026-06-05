using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Purchasing.UseCases.Retentions;

/// <summary>Línea de retención a aplicar — código SRI + tipo de impuesto.</summary>
public sealed record RetentionLineInput(string SriCode, string TaxType);

public sealed record GenerateIssuedRetentionCommand(
    Guid PurchBillId,
    IReadOnlyList<RetentionLineInput> Lines
) : IRequest<Result<Guid>>, ICompanyScopedRequest;
