using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Contabilidad.UseCases.ConfiguracionContable;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record DeleteConfiguracionGastoCategoriaCommand(Guid Id) : IRequest<Result<Unit>>;
