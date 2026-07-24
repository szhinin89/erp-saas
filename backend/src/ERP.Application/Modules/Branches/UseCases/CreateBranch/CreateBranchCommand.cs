using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;

namespace ERP.Application.Modules.Branches.UseCases.CreateBranch;

public sealed record CreateBranchCommand(
    string   Name,
    string   Address,
    string?  Description,
    string?  Reference,
    string?  PostalCode,
    string?  Phone,
    string?  SecondaryPhone,
    string?  Email,
    string?  Website,
    string?  ManagerName,
    string?  ManagerPosition,
    string?  ManagerEmail,
    string?  ManagerPhone,
    string?  CountryId,
    string?  ProvinceId,
    string?  CantonId,
    string?  ParishId,
    string?  Latitude,
    string?  Longitude,
    DateOnly? OpeningDate,
    string?  InternalNotes,
    bool     IsActive,
    bool     IsMainBranch) : IRequest<Result<BranchListItemDto>>, ICompanyScopedRequest;
