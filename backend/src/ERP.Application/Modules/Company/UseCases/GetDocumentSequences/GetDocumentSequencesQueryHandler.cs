using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.GetDocumentSequences;

public sealed class GetDocumentSequencesQueryHandler
    : IRequestHandler<GetDocumentSequencesQuery, Result<IReadOnlyList<DocumentSequenceDto>>>
{
    private readonly IDocumentSequenceRepository _sequenceRepo;

    public GetDocumentSequencesQueryHandler(IDocumentSequenceRepository sequenceRepo) =>
        _sequenceRepo = sequenceRepo;

    public async Task<Result<IReadOnlyList<DocumentSequenceDto>>> Handle(
        GetDocumentSequencesQuery request,
        CancellationToken cancellationToken
    )
    {
        var sequences = await _sequenceRepo.GetAllAsync(cancellationToken);

        return Result<IReadOnlyList<DocumentSequenceDto>>.Success(
            sequences
                .Select(s => new DocumentSequenceDto(
                    s.EmissionPointId,
                    s.DocTypeCode,
                    s.CurrentSeq,
                    s.HasBeenUsed,
                    s.UpdatedAt
                ))
                .ToList()
        );
    }
}
