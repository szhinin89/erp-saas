using ERP.Application.Common;
using ERP.Application.Modules.Settings.Operations.DTOs;
using MediatR;

namespace ERP.Application.Modules.Settings.Operations.UseCases.GetOperationalPreferences;

public sealed record GetOperationalPreferencesQuery : IRequest<Result<OperationalPreferencesDto>>;
