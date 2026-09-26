using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Configuration.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Payables;

/// <summary>
/// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — el formulario de pagos lee la política "pago sin CxP"
/// desde el propio módulo (resuelta por la empresa del contexto, nunca de otra).
/// </summary>
public sealed class GetSupplierPaymentPolicyUseCasesTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Expone_la_politica_resuelta_para_la_empresa_del_contexto(bool allow)
    {
        var resolver = new Mock<IOperationalPreferencesResolver>();
        resolver
            .Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new OperationalPreferences(
                    null!,
                    null!,
                    new PurchasesPreferences(null, true, true, true, false),
                    null!,
                    null!,
                    null!,
                    null!,
                    new PayablesPreferences(allow)
                )
            );

        var result = await new GetSupplierPaymentPolicyHandler(resolver.Object)
            .Handle(new GetSupplierPaymentPolicyQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AllowWithoutPayable.Should().Be(allow);
    }
}
