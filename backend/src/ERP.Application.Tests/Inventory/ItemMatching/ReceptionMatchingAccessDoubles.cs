using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using Moq;

namespace ERP.Application.Tests.Inventory.ItemMatching;

/// <summary>
/// Doble de <see cref="IReceptionMatchingAccess"/> para las pruebas de comportamiento de conciliación
/// (el permiso contextual se prueba en <c>ReceptionMatchingAccessTests</c> y en las pruebas H3).
/// </summary>
internal static class ReceptionMatchingAccessDoubles
{
    public static IReceptionMatchingAccess AllowAll() =>
        Mock.Of<IReceptionMatchingAccess>(a =>
            a.CanMatchInAnyContextAsync(It.IsAny<CancellationToken>()) == Task.FromResult(true)
            && a.DecideAsync(It.IsAny<PurchaseReceptionDocument>(), It.IsAny<CancellationToken>())
                == Task.FromResult(ReceptionMatchingDecision.Allowed)
        );
}
