using System.Reflection;
using ERP.API.Controllers;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace ERP.Architecture.Tests;

/// <summary>
/// ZH-SUPPLIER-PAYMENT-CASH-OWNERSHIP-02B — la autoridad sobre una <c>CashSession</c> exige DOS
/// condiciones independientes: el permiso de la acción (política en el endpoint) Y ser quien opera
/// la sesión (<c>CashSession.IsControlledBy</c>, verificado en el caso de uso). Los tests de
/// Application prueban que el permiso sin propiedad no basta; este gate prueba lo inverso — que
/// la propiedad sin permiso tampoco basta — asegurando que ningún endpoint que mueve una sesión
/// pierda su política de permiso (quitarla dejaría la propiedad como única barrera).
/// </summary>
public sealed class CashSessionOwnershipPolicyTests
{
    public static TheoryData<Type, string, string> SessionMutatingEndpoints =>
        new()
        {
            { typeof(CashSessionController), nameof(CashSessionController.Close), CajaPermissions.Close },
            { typeof(CashSessionController), nameof(CashSessionController.RecordMovement), CajaPermissions.Record },
            { typeof(SupplierPaymentsController), nameof(SupplierPaymentsController.Register), SupplierPaymentsPermissions.Create },
            { typeof(SupplierPaymentsController), nameof(SupplierPaymentsController.Reverse), SupplierPaymentsPermissions.Reverse },
            { typeof(SupplierCreditController), nameof(SupplierCreditController.Refund), FinancePermissions.Update },
            { typeof(SupplierCreditController), nameof(SupplierCreditController.ReverseRefund), FinancePermissions.Update },
        };

    [Theory]
    [MemberData(nameof(SessionMutatingEndpoints))]
    public void Endpoint_que_mueve_una_CashSession_exige_su_permiso(Type controller, string action, string permission)
    {
        var method = controller.GetMethod(action, BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull($"{controller.Name}.{action} debe existir");

        var policies = method!
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(a => a.Policy)
            .ToList();

        policies.Should().Contain($"perm:{permission}",
            $"{controller.Name}.{action} mueve una CashSession: la propiedad de la sesión nunca reemplaza al permiso");
    }
}
