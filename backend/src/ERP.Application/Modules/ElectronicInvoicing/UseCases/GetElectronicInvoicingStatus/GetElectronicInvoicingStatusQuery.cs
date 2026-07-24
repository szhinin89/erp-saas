using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.GetElectronicInvoicingStatus;

public sealed record GetElectronicInvoicingStatusQuery : IRequest<Result<ElectronicInvoicingStatusDto>>;
