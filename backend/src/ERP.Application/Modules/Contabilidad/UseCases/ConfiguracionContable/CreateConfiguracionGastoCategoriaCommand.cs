using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;

namespace ERP.Application.Modules.Contabilidad.UseCases.ConfiguracionContable;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record CreateConfiguracionGastoCategoriaCommand(
    string Categoria,
    Guid CuentaGastoId
) : IRequest<Result<ConfiguracionGastoCategoriaDto>>;
