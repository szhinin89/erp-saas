using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetSupplierRetentionDefaults;

public sealed class GetSupplierRetentionDefaultsHandler
    : IRequestHandler<
        GetSupplierRetentionDefaultsQuery,
        Result<IReadOnlyList<SupplierRetentionDefaultDto>>
    >
{
    private readonly ISupplierRetentionDefaultRepository _repo;
    private readonly ISriCatalogLookupRepository _catalogRepo;

    public GetSupplierRetentionDefaultsHandler(
        ISupplierRetentionDefaultRepository repo,
        ISriCatalogLookupRepository catalogRepo
    )
    {
        _repo = repo;
        _catalogRepo = catalogRepo;
    }

    public async Task<Result<IReadOnlyList<SupplierRetentionDefaultDto>>> Handle(
        GetSupplierRetentionDefaultsQuery q,
        CancellationToken cancellationToken
    )
    {
        var entries = await _repo.GetByBusinessPartnerAsync(q.BusinessPartnerId, cancellationToken);

        var dtos = new List<SupplierRetentionDefaultDto>(entries.Count);
        foreach (var entry in entries)
        {
            var catalogCode = await _catalogRepo.GetRetentionCodeByIdAsync(
                entry.SriRetentionCodeId,
                cancellationToken
            );
            dtos.Add(SupplierRetentionDefaultDto.From(entry, catalogCode));
        }

        return Result<IReadOnlyList<SupplierRetentionDefaultDto>>.Success(dtos);
    }
}
