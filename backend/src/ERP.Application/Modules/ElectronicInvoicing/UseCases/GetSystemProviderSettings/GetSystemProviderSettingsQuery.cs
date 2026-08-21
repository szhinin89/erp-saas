using ERP.Application.Common;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using MediatR;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.GetSystemProviderSettings;

/// <summary>No es company-scoped — el proveedor de sistema es único por instancia del ERP.</summary>
public sealed record GetSystemProviderSettingsQuery : IRequest<Result<SystemProviderSettingsDto>>;
