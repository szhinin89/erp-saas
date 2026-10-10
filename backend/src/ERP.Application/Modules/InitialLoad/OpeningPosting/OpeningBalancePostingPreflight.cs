using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.InitialLoad.Constants;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;

namespace ERP.Application.Modules.InitialLoad.OpeningPosting;

public sealed record OpeningBalancePostingIssue(string Code, string Message);

/// <summary>
/// Resultado de <see cref="OpeningBalancePostingPreflight.CheckAsync"/>. <see cref="Fact"/> es el
/// hecho contable que se publicaría (null si el lote no llega a tenerlo); <see cref="IsReady"/>
/// solo si no hay ningún bloqueo.
/// </summary>
public sealed record OpeningBalancePostingPreflightResult(
    Guid ImportBatchId,
    PostingFact? Fact,
    OpeningBalancePostingStatus? CurrentStatus,
    IReadOnlyList<OpeningBalancePostingIssue> Issues
)
{
    public bool IsReady => Issues.Count == 0;
}

/// <summary>
/// IL-7A — validaciones previas al asiento de apertura de un lote (sin generarlo; IL-7B lo publica).
/// Lote de saldos <c>Completed</c> de la empresa activa, no contabilizado aún,
/// <c>Company.OpeningBalanceDate</c> definida y coincidente con el corte registrado, monto &gt; 0
/// tomado de los datos confirmados del dominio (<see cref="IOpeningBalanceSourceReader"/>) y
/// redondeado con el estándar monetario (<see cref="OpeningBalancePosting.RoundAmount"/>); luego el
/// mismo pipeline del Posting Engine en seco (<see cref="PostingPreflight"/>): regla
/// <c>InitialLoad/*</c> activa, período abierto (PERIOD_NOT_OPEN nunca se salta), cuentas postables,
/// partida doble. Los permisos (confirmación InitialLoad + permiso contable) los exige el comando
/// de IL-7B.
/// </summary>
public sealed class OpeningBalancePostingPreflight
{
    private readonly IOperationalContext _ctx;
    private readonly IImportBatchRepository _batches;
    private readonly IOpeningBalancePostingRepository _postings;
    private readonly IOpeningBalanceConstraintsReader _openingBalance;
    private readonly IOpeningBalanceSourceReader _sources;
    private readonly PostingPreflight _postingPreflight;

    public OpeningBalancePostingPreflight(
        IOperationalContext ctx,
        IImportBatchRepository batches,
        IOpeningBalancePostingRepository postings,
        IOpeningBalanceConstraintsReader openingBalance,
        IOpeningBalanceSourceReader sources,
        PostingPreflight postingPreflight
    )
    {
        _ctx = ctx;
        _batches = batches;
        _postings = postings;
        _openingBalance = openingBalance;
        _sources = sources;
        _postingPreflight = postingPreflight;
    }

    /// <summary>IL-8E — <see cref="InitialLoadClosedGuard"/> para la contabilización por lote (IL-7B).</summary>
    public Task<string?> CheckInitialLoadOpenAsync(CancellationToken ct) =>
        InitialLoadClosedGuard.CheckOpenAsync(_openingBalance, ct);

    public async Task<OpeningBalancePostingPreflightResult> CheckAsync(Guid importBatchId, CancellationToken ct)
    {
        var issues = new List<OpeningBalancePostingIssue>();
        OpeningBalancePostingPreflightResult Result(PostingFact? fact = null, OpeningBalancePostingStatus? status = null) =>
            new(importBatchId, fact, status, issues);

        var batch = await _batches.GetByIdAsync(importBatchId, _ctx.TenantId, _ctx.CompanyId, ct);
        if (batch is null)
        {
            issues.Add(new("BATCH_NOT_FOUND", "Lote de importación no encontrado."));
            return Result();
        }
        var factType = OpeningBalancePostingFacts.ForImportType(batch.ImportType);
        if (factType is null)
        {
            issues.Add(new("BATCH_NOT_ACCOUNTABLE", "Este tipo de carga inicial no genera asiento de apertura."));
            return Result();
        }
        if (batch.Status != ImportStatus.Completed)
        {
            issues.Add(new("BATCH_NOT_COMPLETED", "Solo un lote confirmado por completo se contabiliza."));
            return Result();
        }

        var existing = await _postings.FindByBatchAsync(_ctx.TenantId, _ctx.CompanyId, batch.Id, ct);
        if (existing?.Status == OpeningBalancePostingStatus.Posted)
        {
            issues.Add(new("ALREADY_POSTED", "La apertura del lote ya está contabilizada."));
            return Result(status: existing.Status);
        }

        var openingDate = await _openingBalance.GetOpeningBalanceDateAsync(ct);
        if (openingDate is null)
        {
            issues.Add(new("OPENING_DATE_MISSING", "La empresa no tiene definida su fecha de apertura de saldos."));
            return Result(status: existing?.Status);
        }

        var source = await _sources.GetConfirmedSourceAsync(batch.Id, batch.ImportType, ct);
        if (source.DocumentCount == 0)
            issues.Add(new("SOURCE_EMPTY", "El lote no tiene saldos confirmados que contabilizar."));
        if (source.MissingCostCount > 0)
            issues.Add(new("SOURCE_COST_MISSING",
                $"{source.MissingCostCount} movimiento(s) de inventario inicial no tienen costo total."));
        if (source.CutoffDates.Any(d => d != openingDate.Value))
            issues.Add(new("OPENING_DATE_MISMATCH",
                $"El corte registrado del lote ({string.Join(", ", source.CutoffDates.Order().Select(d => d.ToString("yyyy-MM-dd")))}) "
                    + $"no coincide con la fecha de apertura de la empresa ({openingDate.Value:yyyy-MM-dd})."));
        var amount = OpeningBalancePosting.RoundAmount(source.RawAmount);
        if (amount <= 0m)
            issues.Add(new("AMOUNT_NOT_POSITIVE", "El monto de apertura del lote debe ser mayor a cero."));
        if (issues.Count > 0)
            return Result(status: existing?.Status);

        var fact = new PostingFact(
            _ctx.TenantId,
            _ctx.CompanyId,
            OpeningBalancePostingFacts.SourceModule,
            factType,
            batch.Id,
            openingDate.Value,
            Subtotal: 0m,
            TotalVat: 0m,
            TotalIce: 0m,
            TotalDiscount: 0m,
            GrandTotal: amount
        );
        var posting = await _postingPreflight.CheckAsync(fact, ct);
        if (!posting.IsSuccess)
            issues.Add(new(posting.Code ?? "POSTING_PREFLIGHT_FAILED", posting.Error!));
        return Result(fact, existing?.Status);
    }
}
