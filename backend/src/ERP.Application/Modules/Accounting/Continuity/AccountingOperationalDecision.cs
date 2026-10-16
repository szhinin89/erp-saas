using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.Continuity;

public enum AccountingApplicability
{
    /// <summary>La Company aún no eligió su modo operativo: no se puede decidir (fail-closed).</summary>
    NotConfigured = 1,

    /// <summary>Ninguna versión Enabled cubre la fecha de negocio: el hecho no genera obligación.</summary>
    NotApplicable = 2,

    /// <summary>Una versión Enabled cubre la fecha de negocio: el hecho exige obligación durable.</summary>
    Applicable = 3,
}

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — única decisión de si un hecho operativo está sujeto a Accounting:
/// versión de <c>AccountingOperationalPolicy</c> de la Company que cubre la fecha de negocio del
/// hecho (docs/architecture/accounting.md §3). <c>Company.IsAccountingReq</c> no se consulta aquí:
/// sus invariantes (Required ⇒ Enabled abierto) se aseguran al cambiar la política o el dato fiscal.
/// </summary>
public sealed class AccountingOperationalDecision
{
    public const string NotConfiguredCode = "ACCOUNTING_MODE_NOT_CONFIGURED";

    public const string NotConfiguredMessage =
        "La empresa no tiene definido el modo operativo de Contabilidad. "
        + "Defínalo en Contabilidad > Configuración > Modo operativo antes de registrar esta operación.";

    private readonly IAccountingOperationalPolicyRepository _policies;

    public AccountingOperationalDecision(IAccountingOperationalPolicyRepository policies) =>
        _policies = policies;

    public async Task<AccountingApplicability> ResolveAsync(
        Guid tenantId,
        Guid companyId,
        DateOnly businessDate,
        CancellationToken ct
    )
    {
        var versions = await _policies.ListAsync(tenantId, companyId, ct);
        if (versions.Count == 0)
            return AccountingApplicability.NotConfigured;

        var version = versions.FirstOrDefault(v => v.Covers(businessDate));
        return version?.Mode == AccountingOperationalMode.Enabled
            ? AccountingApplicability.Applicable
            : AccountingApplicability.NotApplicable;
    }
}
