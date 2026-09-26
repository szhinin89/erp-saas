using ERP.Application.Common;
using ERP.Domain.Configuration.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Payables.UseCases;

/// <summary>
/// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — política de la empresa que el formulario de pagos
/// necesita conocer, expuesta por el propio módulo (permiso <c>supplier-payments.create</c>): quien
/// registra pagos no necesita <c>settings.operations.view</c> para saber si puede pagar sin CxP.
/// Solo lectura y solo UX — <c>RegisterSupplierPaymentCommandHandler</c> vuelve a resolver y aplicar
/// la política siempre.
/// </summary>
public sealed record SupplierPaymentPolicyDto(bool AllowWithoutPayable);

public sealed record GetSupplierPaymentPolicyQuery
    : IRequest<Result<SupplierPaymentPolicyDto>>,
        ICompanyScopedRequest;

public sealed class GetSupplierPaymentPolicyHandler
    : IRequestHandler<GetSupplierPaymentPolicyQuery, Result<SupplierPaymentPolicyDto>>
{
    private readonly IOperationalPreferencesResolver _preferences;

    public GetSupplierPaymentPolicyHandler(IOperationalPreferencesResolver preferences) =>
        _preferences = preferences;

    public async Task<Result<SupplierPaymentPolicyDto>> Handle(
        GetSupplierPaymentPolicyQuery request,
        CancellationToken cancellationToken
    )
    {
        var preferences = await _preferences.ResolveAsync(cancellationToken);
        return Result<SupplierPaymentPolicyDto>.Success(
            new SupplierPaymentPolicyDto(preferences.Payables?.AllowSupplierPaymentWithoutPayable ?? false)
        );
    }
}
