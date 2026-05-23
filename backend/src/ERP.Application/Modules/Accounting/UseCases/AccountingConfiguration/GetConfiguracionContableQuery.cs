using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;

namespace ERP.Application.Modules.Accounting.UseCases.ConfiguracionContable;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record GetConfigurationContableQuery : IRequest<Result<AccountingSetupDto?>>, ICompanyScopedRequest;
