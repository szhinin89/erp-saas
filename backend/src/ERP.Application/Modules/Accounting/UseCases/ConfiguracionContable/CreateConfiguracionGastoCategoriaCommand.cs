using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;

namespace ERP.Application.Modules.Accounting.UseCases.ConfiguracionContable;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record CreateConfiguracionGastoCategoriaCommand(
    string Categoria,
    Guid CuentaGastoId
) : IRequest<Result<ConfiguracionGastoCategoriaDto>>;
