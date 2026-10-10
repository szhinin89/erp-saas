using System.Globalization;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.InitialLoad.Constants;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.InitialLoad.OpeningPosting;

/// <summary>Línea Debe/Haber del ASI de apertura: exactamente uno de los dos montos &gt; 0.</summary>
public sealed record OpeningJournalEntryLineInput(
    Guid AccountId,
    decimal Debit,
    decimal Credit,
    string? Description = null
);

/// <summary>Estado del ASI de apertura tras <see cref="PublishOpeningJournalEntryCommand"/>.</summary>
public sealed record OpeningJournalEntryPostingDto(
    Guid Id,
    int Version,
    DateOnly EntryDate,
    decimal TotalAmount,
    int LineCount,
    OpeningBalancePostingStatus Status,
    Guid? JournalEntryId,
    DateTime? PostedAt,
    int Attempts,
    bool AlreadyPosted
);

/// <summary>
/// IL-8A — publica el ÚNICO ASI de apertura de la empresa activa: reclasifica la cuenta puente
/// "Saldos de apertura" a sus cuentas definitivas con líneas Debe/Haber dinámicas, a la fecha
/// <c>Company.OpeningBalanceDate</c>, vía el Posting Engine existente (hecho
/// <c>InitialLoad</c>/<see cref="OpeningBalancePostingFacts.OpeningJournalEntry"/>, regla
/// habilitadora sin líneas fijas, todas las líneas como <see cref="PostingAllocation"/>,
/// <c>SourceEventId</c> = id del <see cref="OpeningJournalEntryPosting"/>). Solo contable: no crea
/// saldos operativos. Sin reverso aquí (IL-8B).
/// </summary>
public sealed record PublishOpeningJournalEntryCommand(IReadOnlyList<OpeningJournalEntryLineInput> Lines)
    : IRequest<Result<OpeningJournalEntryPostingDto>>,
        ICompanyScopedRequest;

public sealed class PublishOpeningJournalEntryCommandValidator
    : AbstractValidator<PublishOpeningJournalEntryCommand>
{
    public const int MaxLines = 500;
    public const int DescriptionMaxLength = 500;

    public PublishOpeningJournalEntryCommandValidator()
    {
        RuleFor(x => x.Lines)
            .NotNull()
            .Must(l => l is { Count: >= 2 and <= MaxLines })
            .WithMessage($"El asiento de apertura necesita entre 2 y {MaxLines} líneas.");
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.AccountId).NotEmpty().WithMessage("Cada línea requiere una cuenta contable.");
                line.RuleFor(l => l)
                    .Must(l => l.Debit >= 0m && l.Credit >= 0m && (l.Debit > 0m) != (l.Credit > 0m))
                    .WithName("Monto")
                    .WithMessage("Cada línea debe tener un monto mayor a cero solo en Debe o solo en Haber.");
                line.RuleFor(l => l)
                    .Must(l => IsMonetary(l.Debit) && IsMonetary(l.Credit))
                    .WithName("Monto")
                    .WithMessage("Los montos admiten como máximo 2 decimales.");
                line.RuleFor(l => l.Description).MaximumLength(DescriptionMaxLength);
            })
            .When(x => x.Lines is not null);
    }

    private static bool IsMonetary(decimal amount) => OpeningBalancePosting.RoundAmount(amount) == amount;
}

/// <summary>
/// Orden: transacción → <c>FOR UPDATE</c> de la empresa y de sus lotes de saldos (serializa con
/// otra publicación, con el cambio de fecha de apertura y con la contabilización IL-7B, que mueven
/// la cuenta puente) → Posted devuelve el resultado existente sin re-ejecutar → fecha de apertura
/// definida → estado Pending/reintento → regla ASI sin líneas fijas → cuentas de control
/// (Inventario/CxC/CxP) y cuenta puente resueltas desde las reglas <c>InitialLoad/*</c> (nunca por
/// código) → ninguna línea en cuenta de control → mismo pipeline del engine en seco (regla activa,
/// período abierto, cuentas activas/de la empresa/postables, partida doble) → la cuenta puente
/// queda exactamente en 0 → <see cref="IPostingEngine.PostAsync"/> → Posted. Cualquier bloqueo deja
/// Failed (reintentable) sin asiento ni número de secuencia.
/// </summary>
public sealed partial class PublishOpeningJournalEntryCommandHandler
    : IRequestHandler<PublishOpeningJournalEntryCommand, Result<OpeningJournalEntryPostingDto>>
{
    public const string OpeningDateMissingCode = "OPENING_DATE_MISSING";
    public const string RuleHasFixedLinesCode = "OPENING_ASI_RULE_HAS_FIXED_LINES";
    public const string ControlRuleMissingCode = "OPENING_CONTROL_RULE_MISSING";
    public const string BridgeAmbiguousCode = "OPENING_BRIDGE_AMBIGUOUS";
    public const string ControlAccountBlockedCode = "OPENING_CONTROL_ACCOUNT_BLOCKED";
    public const string BridgeNotClearedCode = "OPENING_BRIDGE_NOT_CLEARED";
    private const string InternalErrorMessage = "Error interno del sistema.";

    private static readonly (string FactType, string Label, AccountNature ControlSide)[] ControlFacts =
    [
        (OpeningBalancePostingFacts.OpeningInventory, "Inventario", AccountNature.Debit),
        (OpeningBalancePostingFacts.OpeningReceivables, "Cuentas por cobrar", AccountNature.Debit),
        (OpeningBalancePostingFacts.OpeningPayables, "Cuentas por pagar", AccountNature.Credit),
    ];

    private readonly IOperationalContext _ctx;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOpeningJournalEntryPostingRepository _postings;
    private readonly IOpeningBalanceConstraintsReader _openingBalance;
    private readonly IPostingRuleRepository _rules;
    private readonly IJournalEntryRepository _journal;
    private readonly PostingPreflight _postingPreflight;
    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<PublishOpeningJournalEntryCommandHandler> _logger;

    public PublishOpeningJournalEntryCommandHandler(
        IOperationalContext ctx,
        IUnitOfWork unitOfWork,
        IOpeningJournalEntryPostingRepository postings,
        IOpeningBalanceConstraintsReader openingBalance,
        IPostingRuleRepository rules,
        IJournalEntryRepository journal,
        PostingPreflight postingPreflight,
        IPostingEngine postingEngine,
        ILogger<PublishOpeningJournalEntryCommandHandler> logger
    )
    {
        _ctx = ctx;
        _unitOfWork = unitOfWork;
        _postings = postings;
        _openingBalance = openingBalance;
        _rules = rules;
        _journal = journal;
        _postingPreflight = postingPreflight;
        _postingEngine = postingEngine;
        _logger = logger;
    }

    public async Task<Result<OpeningJournalEntryPostingDto>> Handle(
        PublishOpeningJournalEntryCommand command,
        CancellationToken ct
    )
    {
        var tenantId = _ctx.TenantId;
        var companyId = _ctx.CompanyId;
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _postings.LockCompanyOpeningAsync(tenantId, companyId, includeBalanceBatches: true, ct);
            var posting = await _postings.FindCurrentAsync(tenantId, companyId, ct);
            if (posting?.Status == OpeningBalancePostingStatus.Posted)
            {
                await _unitOfWork.CommitAsync(ct);
                return Result<OpeningJournalEntryPostingDto>.Success(ToDto(posting, alreadyPosted: true));
            }

            var openingDate = await _openingBalance.GetOpeningBalanceDateAsync(ct);
            if (openingDate is null)
                return await FailAsync(posting, OpeningDateMissingCode,
                    "La empresa no tiene definida su fecha de apertura de saldos.", ct);

            var lines = command.Lines;
            var total = lines.Sum(l => l.Debit);
            if (posting is null)
            {
                // Versión nueva (Id = SourceEventId nuevo) solo si no hay vigente; los reintentos de
                // la vigente conservan su Id. El reemplazo de una publicada es IL-8B.
                var version = await _postings.GetLastVersionAsync(tenantId, companyId, ct) + 1;
                posting = OpeningJournalEntryPosting.CreatePending(
                    tenantId, companyId, version, openingDate.Value, total, lines.Count, _ctx.UserId);
                await _postings.AddAsync(posting, ct);
            }
            else
            {
                posting.PrepareAttempt(openingDate.Value, total, lines.Count, _ctx.UserId);
            }

            var asiRule = await _rules.FindByKeyAsync(tenantId, companyId,
                OpeningBalancePostingFacts.SourceModule, OpeningBalancePostingFacts.OpeningJournalEntry, ct);
            if (asiRule is { Lines.Count: > 0 })
                return await FailAsync(posting, RuleHasFixedLinesCode,
                    "La regla de contabilización del asiento de apertura tiene líneas fijas; "
                        + "debe ser solo habilitadora (todas las líneas las define el asiento).", ct);

            var (controls, bridgeId, accountsIssue) = await ResolveAccountsAsync(tenantId, companyId, ct);
            if (accountsIssue is not null)
                return await FailAsync(posting, accountsIssue.Code, accountsIssue.Message, ct);
            var blocked = lines.Select(l => l.AccountId).Where(controls.ContainsKey).Distinct().ToList();
            if (blocked.Count > 0)
                return await FailAsync(posting, ControlAccountBlockedCode,
                    "El asiento de apertura no puede mover cuentas de control de "
                        + string.Join(", ", blocked.Select(id => controls[id]).Distinct())
                        + ": sus saldos de apertura se cargan desde Carga Inicial.", ct);

            var fact = new PostingFact(
                tenantId,
                companyId,
                OpeningBalancePostingFacts.SourceModule,
                OpeningBalancePostingFacts.OpeningJournalEntry,
                posting.Id,
                openingDate.Value,
                Subtotal: 0m,
                TotalVat: 0m,
                TotalIce: 0m,
                TotalDiscount: 0m,
                GrandTotal: posting.TotalAmount,
                Allocations: lines.Select(ToAllocation).ToList()
            );
            var check = await _postingPreflight.CheckAsync(fact, ct);
            if (!check.IsSuccess)
                return await FailAsync(posting, check.Code ?? "POSTING_PREFLIGHT_FAILED", check.Error!, ct);

            var bridgeTotals = await _journal.GetAccountLineTotalsAsync(
                tenantId, companyId, null, null, [bridgeId], ct);
            bridgeTotals.TryGetValue(bridgeId, out var current);
            var bridgeAfter = OpeningBalancePosting.RoundAmount(
                current.TotalCredit - current.TotalDebit
                    + lines.Where(l => l.AccountId == bridgeId).Sum(l => l.Credit - l.Debit));
            if (bridgeAfter != 0m)
                return await FailAsync(posting, BridgeNotClearedCode,
                    $"La cuenta de Saldos de apertura quedaría con saldo {Money(bridgeAfter)} "
                        + $"(saldo actual {Money(current.TotalCredit - current.TotalDebit)}); el asiento debe dejarla exactamente en 0.",
                    ct);

            var outcome = await _postingEngine.PostAsync(fact, ct);
            if (!outcome.IsSuccess)
                return await FailAsync(posting, outcome.Code ?? "POSTING_FAILED", outcome.Error!, ct);

            posting.MarkPosted(outcome.Value!.JournalEntryId, _ctx.UserId);
            await _postings.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<OpeningJournalEntryPostingDto>.Success(ToDto(posting, alreadyPosted: false));
        }
        catch (OperationCanceledException)
        {
            await RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await RollbackAsync();
            LogPublishFailed(ex, companyId);
            return Result<OpeningJournalEntryPostingDto>.Failure(InternalErrorMessage);
        }
    }

    private sealed record AccountsIssue(string Code, string Message);

    /// <summary>
    /// Cuentas de control (lado de control de cada regla <c>InitialLoad/*</c> de saldos, con su
    /// etiqueta) y cuenta puente (contrapartida común de esas reglas). Fail-closed: sin las tres
    /// reglas, o con contrapartidas distintas, no se puede garantizar el bloqueo ni el cuadre.
    /// </summary>
    private async Task<(Dictionary<Guid, string> Controls, Guid BridgeId, AccountsIssue? Issue)> ResolveAccountsAsync(
        Guid tenantId, Guid companyId, CancellationToken ct)
    {
        var controls = new Dictionary<Guid, string>();
        var bridges = new HashSet<Guid>();
        foreach (var (factType, label, side) in ControlFacts)
        {
            var rule = await _rules.FindByKeyAsync(tenantId, companyId, OpeningBalancePostingFacts.SourceModule, factType, ct);
            var controlIds = rule?.Lines.Where(l => l.Nature == side).Select(l => l.AccountId).ToList() ?? [];
            var bridgeIds = rule?.Lines.Where(l => l.Nature != side).Select(l => l.AccountId).ToList() ?? [];
            if (controlIds.Count == 0 || bridgeIds.Count == 0)
                return (controls, Guid.Empty, new AccountsIssue(ControlRuleMissingCode,
                    $"No se puede resolver la cuenta de control de {label}: falta la regla "
                        + $"{OpeningBalancePostingFacts.SourceModule}/{factType} o está incompleta."));
            foreach (var id in controlIds)
                controls.TryAdd(id, label);
            bridges.UnionWith(bridgeIds);
        }

        return bridges.Count == 1
            ? (controls, bridges.Single(), null)
            : (controls, Guid.Empty, new AccountsIssue(BridgeAmbiguousCode,
                "Las reglas de apertura no comparten una única cuenta puente de Saldos de apertura."));
    }

    private static string Money(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static PostingAllocation ToAllocation(OpeningJournalEntryLineInput line) =>
        line.Debit > 0m
            ? new PostingAllocation(line.AccountId, line.Debit, AccountNature.Debit, line.Description)
            : new PostingAllocation(line.AccountId, line.Credit, AccountNature.Credit, line.Description);

    /// <summary>
    /// Registra el intento fallido (si ya hay estado) y CONFIRMA la transacción: el estado Failed
    /// queda persistido para el reintento; nunca se crea asiento ni se reserva número.
    /// </summary>
    private async Task<Result<OpeningJournalEntryPostingDto>> FailAsync(
        OpeningJournalEntryPosting? posting, string code, string message, CancellationToken ct)
    {
        if (posting is not null)
        {
            posting.MarkFailed(code, message, _ctx.UserId);
            await _postings.SaveChangesAsync(ct);
        }
        await _unitOfWork.CommitAsync(ct);
        return Result<OpeningJournalEntryPostingDto>.ValidationFailure(message, code);
    }

    private async Task RollbackAsync()
    {
        await _unitOfWork.RollbackAsync(CancellationToken.None);
        _unitOfWork.ClearChangeTracker();
    }

    private static OpeningJournalEntryPostingDto ToDto(OpeningJournalEntryPosting p, bool alreadyPosted) =>
        new(p.Id, p.Version, p.EntryDate, p.TotalAmount, p.LineCount, p.Status, p.JournalEntryId, p.PostedAt, p.Attempts,
            alreadyPosted);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Publicación del asiento de apertura de la empresa {CompanyId} falló por un error interno"
    )]
    private partial void LogPublishFailed(Exception ex, Guid companyId);
}
