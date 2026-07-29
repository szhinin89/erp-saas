using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.GetEstablishments;

/// <summary>Devuelve todos los establecimientos activos de la empresa — usado para poblar el selector en formularios.</summary>
public record GetEstablishmentLookupsQuery : IRequest<Result<IReadOnlyList<EstablishmentLookupDto>>>, ICompanyScopedRequest;
