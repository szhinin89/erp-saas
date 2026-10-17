using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Events;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>
/// Regla de contabilización — configuración de datos, nunca lógica condicional cerrada por
/// tipo de documento (ADR-026 §6.2: prohibido <c>if (documentType == "Factura")</c> o
/// equivalentes en el Posting Engine). <b>PostingRule almacena únicamente configuración de
/// mapeo</b> (SourceModule/FactType/Lines) — la resolución de qué regla aplica a un hecho
/// contable concreto y la generación del <c>JournalEntry</c> resultante pertenecen
/// exclusivamente al Posting Engine (ADR-026 §8), nunca a este aggregate.
/// <see cref="PostingRuleLine.AccountId"/> es un identificador plano — este aggregate no
/// referencia la clase <c>Account</c> ni inyecta ningún repositorio; su existencia y pertenencia
/// a la misma <c>Company</c> se validan en Application al guardar y en el Posting Engine al
/// resolver (ver <see cref="Account"/>, <see cref="AccountingPeriod"/>). ACCOUNTING-11: los campos
/// planos heredados DebitAccountId/CreditAccountId/TaxCode se eliminaron — el motor nunca los
/// leyó; <see cref="Lines"/> es la única configuración efectiva.
/// </summary>
public sealed class PostingRule : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public string SourceModule { get; private set; } = null!;
    public string FactType { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;

    private readonly List<PostingRuleLine> _lines = new();
    public IReadOnlyCollection<PostingRuleLine> Lines => _lines.AsReadOnly();

    private PostingRule() { }

    public static PostingRule Create(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        string factType,
        Guid createdBy
    )
    {
        if (string.IsNullOrWhiteSpace(sourceModule))
            throw new ArgumentException(
                "El módulo de origen es obligatorio.",
                nameof(sourceModule)
            );
        if (string.IsNullOrWhiteSpace(factType))
            throw new ArgumentException(
                "El tipo de hecho contable es obligatorio.",
                nameof(factType)
            );

        var rule = new PostingRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            SourceModule = sourceModule.Trim(),
            FactType = factType.Trim(),
            IsActive = true,
        };
        rule.SetCreated(createdBy);
        rule.RaiseDomainEvent(new PostingRuleCreatedEvent(tenantId, rule.Id, companyId));
        return rule;
    }

    public void Enable(Guid updatedBy)
    {
        if (IsActive)
            throw new DomainRuleViolationException("La regla ya está activa.");
        IsActive = true;
        SetUpdated(updatedBy);
    }

    /// <summary>Baja lógica — nunca DELETE físico (regla general del proyecto).</summary>
    public void Disable(Guid updatedBy)
    {
        if (!IsActive)
            throw new DomainRuleViolationException("La regla ya está deshabilitada.");
        IsActive = false;
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// ACCOUNTING-11 — reemplaza por completo las líneas efectivas (lo único que lee el Posting
    /// Engine). La forma y las cuentas se validan en Application contra el catálogo de hechos; aquí
    /// solo se reconstruye la colección en el orden recibido. No cambia SourceModule/FactType
    /// (identidad) ni IsActive, y no toca asientos ya contabilizados: solo afecta contabilizaciones
    /// futuras.
    /// </summary>
    public void ReplaceLines(
        IEnumerable<(Guid AccountId, AccountNature Nature, PostingAmountKind AmountKind)> lines,
        Guid updatedBy
    )
    {
        _lines.Clear();
        foreach (var (accountId, nature, amountKind) in lines)
            AddLine(accountId, nature, amountKind);
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// Agrega un mapeo de cuenta a la regla (Fase 3.5.3) — configuración de datos, sin resolver
    /// ni validar la existencia de <paramref name="accountId"/> (ver remarks de la clase).
    /// </summary>
    public void AddLine(Guid accountId, AccountNature nature, PostingAmountKind amountKind)
    {
        var line = PostingRuleLine.Create(
            Id,
            TenantId,
            accountId,
            nature,
            amountKind,
            (short)_lines.Count
        );
        _lines.Add(line);
    }

    /// <summary>
    /// RETENTIONS-TAX-COMPONENT-POSTING-02C — quita una línea existente por su cuenta/naturaleza/
    /// tipo de monto exactos, sin resolver ni validar nada nuevo (mismo alcance que
    /// <see cref="AddLine"/>). Usado exclusivamente por backfills de infraestructura que migran una
    /// regla sembrada con una forma antigua conocida a su forma vigente (ver
    /// <c>AccountingBootstrapStep.CorrectLegacyPostingRulesAsync</c>) — nunca por edición manual de
    /// un admin, que no expone este método. No hace nada si no encuentra una línea que coincida
    /// exactamente (idempotente: correrlo dos veces no falla ni borra de más).
    /// </summary>
    public void RemoveLine(Guid accountId, AccountNature nature, PostingAmountKind amountKind)
    {
        var line = _lines.FirstOrDefault(l =>
            l.AccountId == accountId && l.Nature == nature && l.AmountKind == amountKind
        );
        if (line is not null)
            _lines.Remove(line);
    }
}
