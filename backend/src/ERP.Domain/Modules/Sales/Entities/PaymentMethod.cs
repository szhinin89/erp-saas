using ERP.Domain.Common;
using ERP.Domain.Modules.Sales.Enums;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class PaymentMethod : MasterEntity, ITenantScopedEntity
{
    public const int MaxCodeLength = 20;
    public const int MaxNameLength = 100;

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool RequiresReference { get; private set; }
    public bool IsCreditAllowed { get; private set; }
    public int SortOrder { get; private set; }

    /// <summary>Esquema de detalle que la UI debe capturar para este método (tarjeta/transferencia/cheque/ninguno).</summary>
    public PaymentMethodDetailType DetailType { get; private set; }

    /// <summary>
    /// SALES-PAYMENT-METHOD-SRI-MAPPING-SSOT-01: código del catálogo real <c>global.sri_payment_method</c>
    /// (formaPago del comprobante electrónico) que corresponde a esta forma de cobro interna —
    /// SSOT único de la relación PaymentMethod → SriPaymentMethodCode. Nullable: un método sin
    /// mapeo configurado cae al default de empresa (<c>invoice.default_payment_method_code</c>)
    /// al momento de emitir — nunca se asume un código por nombre/heurística. Validado contra el
    /// catálogo activo en <see cref="ERP.Application.Modules.Sales.UseCases.CreatePaymentMethodValidator"/>/
    /// <see cref="ERP.Application.Modules.Sales.UseCases.UpdatePaymentMethodValidator"/>, nunca contra
    /// un HashSet fijo en Domain.
    /// </summary>
    public string? SriPaymentMethodCode { get; private set; }

    private PaymentMethod() { }

    public static PaymentMethod Create(
        Guid tenantId,
        string code,
        string name,
        bool requiresReference,
        bool isCreditAllowed,
        int sortOrder,
        Guid createdBy,
        PaymentMethodDetailType detailType = PaymentMethodDetailType.None,
        string? sriPaymentMethodCode = null
    )
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código es obligatorio.", nameof(code));
        if (code.Length > MaxCodeLength)
            throw new ArgumentException(
                $"El código no puede superar {MaxCodeLength} caracteres.",
                nameof(code)
            );
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (name.Length > MaxNameLength)
            throw new ArgumentException(
                $"El nombre no puede superar {MaxNameLength} caracteres.",
                nameof(name)
            );

        var pm = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            RequiresReference = requiresReference,
            IsCreditAllowed = isCreditAllowed,
            SortOrder = sortOrder,
            DetailType = detailType,
            SriPaymentMethodCode = string.IsNullOrWhiteSpace(sriPaymentMethodCode)
                ? null
                : sriPaymentMethodCode.Trim(),
        };
        pm.SetCreated(createdBy);
        return pm;
    }

    /// <summary>
    /// Fábrica exclusiva del Bootstrap de empresa: idéntica a <see cref="Create"/> pero marca el
    /// registro como sembrado por el sistema (<see cref="MasterEntity.IsSystemSeeded"/>) — Tipo A
    /// (formas de pago por defecto), ver política de Bootstrap en <c>CLAUDE.md</c>. A diferencia de
    /// otros registros Tipo A, aquí el flag es solo informativo: son 5 opciones intercambiables, no un
    /// singleton — deshabilitar una (p. ej. la empresa no acepta cheques) es una decisión de negocio
    /// legítima, no una destrucción accidental de infraestructura. Por eso ningún método de esta
    /// entidad invoca <see cref="SystemSeedGuard.EnsureEditable"/>.
    /// </summary>
    public static PaymentMethod CreateSystemSeeded(
        Guid tenantId,
        string code,
        string name,
        bool requiresReference,
        bool isCreditAllowed,
        int sortOrder,
        Guid createdBy,
        PaymentMethodDetailType detailType = PaymentMethodDetailType.None,
        string? sriPaymentMethodCode = null
    )
    {
        var pm = Create(
            tenantId,
            code,
            name,
            requiresReference,
            isCreditAllowed,
            sortOrder,
            createdBy,
            detailType,
            sriPaymentMethodCode
        );
        pm.MarkAsSystemSeeded();
        return pm;
    }

    public void Update(
        string name,
        bool requiresReference,
        bool isCreditAllowed,
        int sortOrder,
        Guid updatedBy,
        PaymentMethodDetailType detailType,
        string? sriPaymentMethodCode = null
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));

        Name = name.Trim();
        RequiresReference = requiresReference;
        IsCreditAllowed = isCreditAllowed;
        SortOrder = sortOrder;
        DetailType = detailType;
        SriPaymentMethodCode = string.IsNullOrWhiteSpace(sriPaymentMethodCode)
            ? null
            : sriPaymentMethodCode.Trim();
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// SALES-PAYMENT-METHOD-SRI-MAPPING-EFECTIVO-WRONG-CODE-01: backfill idempotente para filas
    /// creadas antes de que existiera SriPaymentMethodCode — asigna el código SOLO si el campo
    /// sigue null. Nunca pisa un mapeo ya configurado (manual o de un backfill previo), a
    /// diferencia de <see cref="Update"/> que sí reemplaza el valor porque representa una edición
    /// explícita del usuario. Usado exclusivamente por <c>PaymentMethodSriMappingBackfillService</c>
    /// (operación de despliegue, no un flujo de usuario).
    /// </summary>
    public bool BackfillSriPaymentMethodCode(string sriPaymentMethodCode, Guid updatedBy)
    {
        if (SriPaymentMethodCode is not null)
            return false;
        if (string.IsNullOrWhiteSpace(sriPaymentMethodCode))
            return false;

        SriPaymentMethodCode = sriPaymentMethodCode.Trim();
        SetUpdated(updatedBy);
        return true;
    }
}
