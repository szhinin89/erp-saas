using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.GetSalesFiscalPolicy;

/// <summary>Lee la política fiscal de Consumidor Final de la empresa activa (tab Fiscal/Tributario).</summary>
public sealed record GetSalesFiscalPolicyQuery : IRequest<Result<SalesFiscalPolicyDto>>;
