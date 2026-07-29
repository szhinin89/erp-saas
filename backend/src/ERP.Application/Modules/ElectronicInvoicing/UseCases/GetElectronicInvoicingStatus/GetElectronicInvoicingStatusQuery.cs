using ERP.Application.Common;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using MediatR;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.GetElectronicInvoicingStatus;

public sealed record GetElectronicInvoicingStatusQuery : IRequest<Result<ElectronicInvoicingStatusDto>>;
