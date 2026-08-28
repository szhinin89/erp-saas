using ERP.Application.Modules.Finance.UseCases.Payments;
using FluentAssertions;

namespace ERP.Application.Tests.Finance;

/// <summary>
/// PAYABLES-PAYMENTS-LEGACY-CLEANUP-14 — guard de regresión: <c>RegisterPaymentCommand</c>/
/// <c>ReversePaymentCommand</c> (registro de pago a proveedor contra <c>AccountsPayable</c>) se
/// eliminaron junto con su única UI (<c>AccountsPayablePage</c>/<c>RegisterPaymentModal</c>, ya
/// eliminadas) y su endpoint (<c>POST /api/v1/finance/payments</c>, también eliminado). Sin
/// PagoCabecera/PagoDetalle todavía, no debe quedar un flujo de pago a proveedor a medias — solo
/// <c>RegisterCollectionCommand</c>/<c>ReverseCollectionCommand</c> (AR, en uso real vía
/// <c>RegisterCollectionModal.tsx</c>) siguen vivos.
/// </summary>
public sealed class PaymentsLegacyCleanupTests
{
    [Fact]
    public void No_existen_referencias_al_flujo_legacy_de_registro_de_pago_a_proveedor()
    {
        var applicationAssembly = typeof(RegisterCollectionCommand).Assembly;
        var forbiddenNames = new[]
        {
            "RegisterPaymentCommand",
            "RegisterPaymentCommandValidator",
            "RegisterPaymentCommandHandler",
            "ReversePaymentCommand",
            "ReversePaymentCommandValidator",
            "ReversePaymentCommandHandler",
            "SupplierPaymentAppliedPostingTranslator",
        };

        var offending = applicationAssembly
            .GetTypes()
            .Where(t => forbiddenNames.Contains(t.Name))
            .Select(t => t.FullName)
            .ToList();

        offending.Should().BeEmpty();
    }

    [Fact]
    public void RegisterCollection_y_ReverseCollection_siguen_vigentes()
    {
        // Confirma que la limpieza no se llevó por delante el flujo de AR (cobros), que sí tiene
        // UI activa (RegisterCollectionModal.tsx / AccountsReceivablePage.tsx).
        var applicationAssembly = typeof(RegisterCollectionCommand).Assembly;
        var liveNames = new[]
        {
            "RegisterCollectionCommand",
            "RegisterCollectionCommandHandler",
            "ReverseCollectionCommand",
            "ReverseCollectionCommandHandler",
        };

        var found = applicationAssembly.GetTypes().Select(t => t.Name).ToHashSet();

        liveNames.Should().AllSatisfy(name => found.Should().Contain(name));
    }
}
