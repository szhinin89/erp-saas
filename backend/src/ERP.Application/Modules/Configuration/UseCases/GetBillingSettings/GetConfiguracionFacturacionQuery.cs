using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using MediatR;

namespace ERP.Application.Configuration.UseCases.GetBillingSettings;

public sealed record GetBillingSettingsQuery : IRequest<Result<BillingSettingsDto?>>;
