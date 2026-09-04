using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.DocTypes.Services;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.Exceptions;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Application.Modules.Retentions.Exceptions;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.DocTypes.Constants;
using ERP.Domain.Modules.DocTypes.Enums;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Retentions.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Expenses.UseCases.Documents;

/// <summary>
/// RETENTIONS-EXPENSES-INTEGRATION-01D-1 — intención OPCIONAL del usuario de generar una retención
/// en la misma operación que confirma el gasto (ver
/// <c>docs/decisions/RETENTIONS-MODULE-DESIGN-01.md</c> § "Flujo funcional integrado de
/// retenciones"). <see cref="AppliesRetention"/> es solo la intención — nunca prueba de que aplica:
/// el servidor siempre revalida elegibilidad contra el documento real vía
/// <see cref="IRetentionIssuer"/> antes de emitir. El monto/base/porcentaje de cada línea siguen sin
/// numeración automática ni cálculo server-side en esta fase (mismo criterio ya documentado en
/// <see cref="IssueRetentionCommand"/> — RETENTIONS-APPLICATION-01C).
/// </summary>
public sealed record RetentionIntent(
    bool AppliesRetention,
    Guid? EmissionPointId,
    string? RetentionNumber,
    DateOnly? IssueDate,
    IReadOnlyList<IssueRetentionLineInput>? Lines
);

/// <summary>Reglas de <see cref="RetentionIntent"/> solo cuando <c>AppliesRetention == true</c> — compartidas por ambos commands de confirmación de gastos.</summary>
public sealed class RetentionIntentValidator : AbstractValidator<RetentionIntent>
{
    public RetentionIntentValidator()
    {
        When(
            x => x.AppliesRetention,
            () =>
            {
                RuleFor(x => x.EmissionPointId)
                    .Must(v => v.HasValue && v.Value != Guid.Empty)
                    .WithMessage("El punto de emisión es obligatorio para generar la retención.");
                RuleFor(x => x.RetentionNumber)
                    .NotEmpty()
                    .WithMessage("El número de retención es obligatorio para generar la retención.");
                RuleFor(x => x.IssueDate)
                    .NotEmpty()
                    .WithMessage("La fecha de emisión de la retención es obligatoria.");
                RuleFor(x => x.Lines)
                    .NotEmpty()
                    .WithMessage("Debe incluir al menos una línea de retención.");
                RuleForEach(x => x.Lines!).SetValidator(new IssueRetentionLineValidator());
            }
        );
    }
}

public sealed record ConfirmExpenseDocumentCommand(Guid Id, RetentionIntent? Retention = null)
    : IRequest<Result<ExpenseDocumentDetailDto>>,
        IBranchScopedRequest;

public sealed class ConfirmExpenseDocumentValidator : AbstractValidator<ConfirmExpenseDocumentCommand>
{
    public ConfirmExpenseDocumentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Retention!).SetValidator(new RetentionIntentValidator()).When(x => x.Retention is not null);
    }
}

/// <summary>
/// RETENTIONS-EXPENSES-INTEGRATION-01D-1 — traduce el fallo de una operación interna
/// (<see cref="Result{TIn}"/>, p. ej. <see cref="IRetentionIssuer"/>) a un <see cref="Result{TOut}"/>
/// del mismo tipo/código que devolvería el handler que la invoca, preservando el <c>Code</c>
/// (Conflict/NotFound/ValidationError/...) para que el controller siga mapeando el status HTTP
/// correcto.
/// </summary>
file static class ResultTranslation
{
    public static Result<TOut> ToFailure<TIn, TOut>(this Result<TIn> source) =>
        source.Code switch
        {
            ApiResponseCodes.Common.Conflict => Result<TOut>.Conflict(source.Error!),
            ApiResponseCodes.Common.NotFound => Result<TOut>.NotFound(source.Error!),
            ApiResponseCodes.Common.Forbidden => Result<TOut>.Forbidden(source.Error!),
            _ => Result<TOut>.ValidationFailure(source.Error!, source.Code),
        };
}

public sealed class ConfirmExpenseDocumentHandler
    : IRequestHandler<ConfirmExpenseDocumentCommand, Result<ExpenseDocumentDetailDto>>
{
    private readonly IExpenseDocumentRepository _repo;
    private readonly IExpenseCategoryRepository _categories;
    private readonly IAccountRepository _accounts;
    private readonly IAccountsPayableService _payables;
    private readonly IDocumentFlowPolicyService _workflowPolicy;
    private readonly IRetentionIssuer _retentionIssuer;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;
    private readonly ILogger<ConfirmExpenseDocumentHandler> _logger;

    public ConfirmExpenseDocumentHandler(
        IExpenseDocumentRepository repo,
        IExpenseCategoryRepository categories,
        IAccountRepository accounts,
        IAccountsPayableService payables,
        IDocumentFlowPolicyService workflowPolicy,
        IRetentionIssuer retentionIssuer,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch,
        ICurrentUser user,
        ILogger<ConfirmExpenseDocumentHandler> logger
    )
    {
        _repo = repo;
        _categories = categories;
        _accounts = accounts;
        _payables = payables;
        _workflowPolicy = workflowPolicy;
        _retentionIssuer = retentionIssuer;
        _tenant = tenant;
        _company = company;
        _logger = logger;
        _branch = branch;
        _user = user;
    }

    public async Task<Result<ExpenseDocumentDetailDto>> Handle(
        ConfirmExpenseDocumentCommand cmd,
        CancellationToken ct
    )
    {
        var document = await _repo.GetByIdAsync(_tenant.TenantId, cmd.Id, ct);
        if (document is null || document.BranchId != _branch.BranchId)
            return Result<ExpenseDocumentDetailDto>.NotFound("Gasto no encontrado.");

        DocumentFlowPolicyResult policy;
        try
        {
            // DOCUMENT-FLOW-POLICY-01: valida CÓMO debe comportarse la confirmación (modo de
            // confirmación/autorización) — el permiso expenses.documents.confirm (QUIÉN puede
            // confirmar) ya se validó en el controller vía [Authorize(Policy = "perm:...")], antes
            // de llegar aquí. Esta llamada nunca reemplaza esa validación de permiso.
            policy = await _workflowPolicy.EnsureConfirmationFlowAsync(
                _company.CompanyId,
                DocTypeCodes.ExpenseDocument,
                ct
            );
        }
        catch (DocumentFlowPolicyViolationException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
        }

        if (document.Status != ExpenseStatus.Draft)
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(
                "Solo se pueden confirmar gastos en estado borrador."
            );
        if (document.SupplierId == Guid.Empty)
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(
                "El gasto debe tener un proveedor para confirmarse."
            );
        if (document.BranchId == Guid.Empty)
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(
                "El gasto debe tener una sucursal valida para confirmarse."
            );
        if (document.Lines.Count == 0)
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(
                "El gasto debe tener al menos una linea para confirmarse."
            );

        var snapshots = new Dictionary<Guid, (Guid AccountId, string? Code, string? Name)>();
        foreach (var line in document.Lines)
        {
            var resolution = await ExpenseConfirmRules.ResolveLineAccountAsync(
                _categories,
                _accounts,
                _tenant.TenantId,
                _company.CompanyId,
                line,
                ct
            );
            if (resolution.Error is not null)
                return resolution.Error.ToResult<ExpenseDocumentDetailDto>();

            snapshots[line.Id] = (
                resolution.Account!.Id,
                resolution.Account.Code.Value,
                resolution.Account.Name
            );
        }

        try
        {
            document.Confirm(snapshots, _user.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
        }

        // RETENTIONS-EXPENSES-INTEGRATION-01D-1/01D-2: si el usuario marcó la intención de generar
        // retención, se construye/emite AQUÍ — ANTES del SaveChangesAsync de abajo y sin llamarlo
        // internamente (IRetentionIssuer solo hace staging, ver su comentario de tipo) — para que
        // los tres agregados (ExpenseDocument confirmado + AccountsPayable staged con la retención
        // aplicada + RetentionDocument emitido) se persistan en un único SaveChangesAsync, atómico
        // por diseño de ErpDbContext.SaveChangesAsync (abre su propia transacción de BD cuando no
        // hay una ambiente y envuelve ahí tanto la escritura inicial como los domain events
        // publicados, incluido el posting de la retención vía
        // RetentionDocumentIssuedPostingTranslator). Si cualquier paso falla, se retorna sin llamar
        // SaveChangesAsync: la mutación en memoria de document.Confirm() de arriba nunca se
        // flushea, así que el gasto NO queda Confirmed en BD — todo o nada, sin necesidad de un
        // IUnitOfWork.BeginTransactionAsync explícito (evita además el riesgo de una transacción
        // anidada, ya que ni RetentionIssuer ni IRetentionEligibilityService abren una propia).
        //
        // 01D-2 cierra el gap dejado explícito por 01D-1: cuando hay retención, la CxP se crea
        // STAGED aquí mismo (vía IAccountsPayableService.StageFromOriginAsync — nunca
        // CreateFromOriginAsync, que comitea por su cuenta) y se le aplica ApplyRetention() ANTES
        // del SaveChangesAsync único, en vez de crearse (bruta, sin retención) en el bloque
        // posterior al posting (ver más abajo, ahora condicionado a que este camino NO se haya
        // ejecutado, para no duplicar la CxP).
        var retentionAppliedToPayable = false;
        if (cmd.Retention is { AppliesRetention: true } retention)
        {
            var retentionResult = await _retentionIssuer.IssueForExpenseAsync(
                document,
                new RetentionIssueRequest(
                    _tenant.TenantId,
                    _company.CompanyId,
                    document.BranchId,
                    _user.UserId,
                    retention.EmissionPointId!.Value,
                    retention.RetentionNumber!,
                    retention.IssueDate!.Value,
                    retention.Lines!
                ),
                ct
            );
            if (!retentionResult.IsSuccess)
                return retentionResult.ToFailure<RetentionDocument, ExpenseDocumentDetailDto>();

            var retentionDocument = retentionResult.Value!;

            // Solo si la política declara PayableGenerationMode.OnConfirmation (mismo criterio que
            // el bloque post-SaveChanges de abajo) hay una CxP a la que aplicar la retención en esta
            // misma operación — si la empresa no genera CxP al confirmar, no hay saldo que netear
            // aquí y la retención queda emitida sin efecto sobre CxP (mismo estado que 01D-1 dejaba
            // para todos los casos).
            if (policy.PayableGenerationMode == PayableGenerationMode.OnConfirmation)
            {
                try
                {
                    var payable = await _payables.StageFromOriginAsync(
                        new CreateAccountsPayableFromOriginRequest(
                            _tenant.TenantId,
                            _company.CompanyId,
                            document.BranchId,
                            document.SupplierId,
                            AccountsPayableOriginType.ExpenseDocument,
                            document.Id,
                            document.DocumentType,
                            document.DocumentNumber,
                            document.IssueDate,
                            document.AccountingDate,
                            new[]
                            {
                                new AccountsPayableInstallmentInput(
                                    1,
                                    document.DueDate ?? document.AccountingDate,
                                    document.GrandTotal
                                ),
                            }
                        ),
                        _user.UserId,
                        ct
                    );
                    payable.ApplyRetention(retentionDocument.TotalRetained, _user.UserId);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    // A diferencia del bloque post-SaveChanges (donde un fallo de CxP no revierte
                    // la confirmación ya persistida), aquí SÍ debe fallar toda la operación: el
                    // usuario pidió explícitamente retención, y una retención emitida sin su CxP
                    // neta aplicada dejaría el saldo del proveedor bruto (incorrecto) — nunca un
                    // estado intermedio (ver docs/decisions/RETENTIONS-MODULE-DESIGN-01.md § "Flujo
                    // funcional integrado de retenciones", punto 8).
                    return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
                }

                retentionAppliedToPayable = true;
            }
        }

        try
        {
            // EXPENSES-CONFIRM-07: a diferencia de Purchases/Sales, el posting de Gastos es
            // estricto — ExpenseDocumentConfirmedPostingTranslator lanza ExpensePostingFailedException
            // (en vez de solo loguear un warning) si IPostingEngine.PostAsync falla. La excepción se
            // propaga desde el Publish() interno de ErpDbContext.SaveChangesAsync, que hace rollback
            // completo de la transacción ANTES de este catch — el documento queda en Draft en BD,
            // nada de lo mutado en memoria (Confirm() de arriba, ni el RetentionDocument/AccountsPayable
            // en staging de arriba) llegó a persistirse.
            // RETENTIONS-EXPENSES-INTEGRATION-01D-2: mismo criterio para el posting de la retención
            // — RetentionDocumentIssuedPostingTranslator lanza RetentionPostingFailedException si
            // falla, capturada abajo, con el mismo efecto de rollback completo.
            // DOCUMENT-FLOW-POLICY-01: la política inicial obligatoria de GASDOC declara
            // AccountingPostingMode.OnConfirmation, que coincide con este comportamiento existente
            // (posting disparado por ExpenseDocumentConfirmedEvent al confirmar). Reestructurar el
            // translator para leer el modo de la política queda fuera de alcance — no rompe nada
            // hoy porque ambos coinciden.
            await _repo.SaveChangesAsync(ct);
        }
        catch (ExpensePostingFailedException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message, ex.Code);
        }
        catch (RetentionPostingFailedException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message, ex.Code);
        }

        // PAYABLES-GENERIC-FOUNDATION-09 + DOCUMENT-FLOW-POLICY-01: "al confirmar gasto, después de
        // posting contable exitoso, crear AccountsPayable" — solo si la política de flujo documental
        // lo declara (PayableGenerationMode.OnConfirmation) Y no se creó ya de forma staged por el
        // camino de retención de arriba (RETENTIONS-EXPENSES-INTEGRATION-01D-2 — evita un
        // AccountsPayable duplicado para el mismo origen). El posting ya se confirmó y persistió
        // arriba (si hubiera fallado, ya habríamos retornado). A diferencia del posting, un fallo
        // aquí NO debe revertir la confirmación ya persistida (el gasto ya tiene asiento contable
        // real) — se registra para seguimiento manual, mismo criterio que Purchases usa para gaps de
        // configuración que no bloquean el documento de origen. CreateFromOriginAsync es
        // idempotente, así que un reintento manual posterior es seguro.
        if (policy.PayableGenerationMode == PayableGenerationMode.OnConfirmation && !retentionAppliedToPayable)
        {
            try
            {
                await _payables.CreateFromOriginAsync(
                    new CreateAccountsPayableFromOriginRequest(
                        _tenant.TenantId,
                        _company.CompanyId,
                        document.BranchId,
                        document.SupplierId,
                        AccountsPayableOriginType.ExpenseDocument,
                        document.Id,
                        document.DocumentType,
                        document.DocumentNumber,
                        document.IssueDate,
                        document.AccountingDate,
                        new[]
                        {
                            new AccountsPayableInstallmentInput(
                                1,
                                document.DueDate ?? document.AccountingDate,
                                document.GrandTotal
                            ),
                        }
                    ),
                    _user.UserId,
                    ct
                );
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo crear la cuenta por pagar para el gasto {ExpenseDocumentId} ({DocumentNumber}) tras confirmar.",
                    document.Id,
                    document.DocumentNumber
                );
            }
        }

        return Result<ExpenseDocumentDetailDto>.Success(ExpenseDocumentMapper.ToDetail(document));
    }
}

/// <summary>
/// EXPENSES-WORKFLOW-INTEGRATION-01: crea un gasto directamente en estado Confirmado (sin pasar
/// por un borrador previo) — mismos datos de entrada que <see cref="CreateExpenseDraftCommand"/>
/// (comparte <see cref="ExpenseDraftHeaderRules{T}"/> vía <see cref="IExpenseDraftInput"/> y la
/// resolución de proveedor/condición de pago/líneas de <see cref="ExpenseDraftRules"/>), pero
/// además ejecuta la misma confirmación/posting/AP que <see cref="ConfirmExpenseDocumentCommand"/>
/// en la misma operación. Bloqueado por <c>IDocumentFlowPolicyService.EnsureDirectCreationAllowedAsync</c>
/// cuando la política de la empresa exige borrador (<see cref="DraftMode.Required"/>) para GASDOC.
/// </summary>
public sealed record CreateConfirmedExpenseCommand(
    Guid SupplierId,
    DateOnly IssueDate,
    DateOnly AccountingDate,
    string DocumentType,
    string DocumentNumber,
    Guid? PaymentTermId,
    DateOnly? DueDate,
    IReadOnlyList<ExpenseDraftLineRequest> Lines,
    string? AuthorizationNumber = null,
    DateTime? AuthorizationDate = null,
    string? Notes = null,
    RetentionIntent? Retention = null
) : IRequest<Result<ExpenseDocumentDetailDto>>, IBranchScopedRequest, IExpenseDraftInput;

/// <summary>
/// RETENTIONS-EXPENSES-INTEGRATION-01D-1: en el vocabulario del negocio, "confirmar" un gasto
/// incluye tanto confirmar un borrador existente (<see cref="ConfirmExpenseDocumentCommand"/>) como
/// crearlo ya confirmado (este command) — ambos caminos terminan en <c>ExpenseStatus.Confirmed</c> y
/// ambos ya comparten el mismo bloque de creación de CxP (ver
/// <c>docs/decisions/RETENTIONS-MODULE-DESIGN-01.md</c> § "Hallazgos técnicos", líneas 178-216/446-484
/// antes de esta fase). Por eso <see cref="RetentionIntent"/> se extiende simétricamente a ambos
/// commands, reutilizando el mismo <see cref="IRetentionIssuer"/>.
/// </summary>
public sealed class CreateConfirmedExpenseValidator : AbstractValidator<CreateConfirmedExpenseCommand>
{
    public CreateConfirmedExpenseValidator()
    {
        Include(new ExpenseDraftHeaderRules<CreateConfirmedExpenseCommand>());
        RuleFor(x => x.Retention!).SetValidator(new RetentionIntentValidator()).When(x => x.Retention is not null);
    }
}

public sealed class CreateConfirmedExpenseHandler
    : IRequestHandler<CreateConfirmedExpenseCommand, Result<ExpenseDocumentDetailDto>>
{
    private readonly IExpenseDocumentRepository _repo;
    private readonly IExpenseCategoryRepository _categories;
    private readonly IAccountRepository _accounts;
    private readonly IBusinessPartnerRepository _businessPartners;
    private readonly IBusinessPartnerRoleRepository _roles;
    private readonly IPaymentTermRepository _paymentTerms;
    private readonly ISriTaxResolver _tax;
    private readonly IAccountsPayableService _payables;
    private readonly IDocumentFlowPolicyService _workflowPolicy;
    private readonly IRetentionIssuer _retentionIssuer;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;
    private readonly ILogger<CreateConfirmedExpenseHandler> _logger;

    public CreateConfirmedExpenseHandler(
        IExpenseDocumentRepository repo,
        IExpenseCategoryRepository categories,
        IAccountRepository accounts,
        IBusinessPartnerRepository businessPartners,
        IBusinessPartnerRoleRepository roles,
        IPaymentTermRepository paymentTerms,
        ISriTaxResolver tax,
        IAccountsPayableService payables,
        IDocumentFlowPolicyService workflowPolicy,
        IRetentionIssuer retentionIssuer,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch,
        ICurrentUser user,
        ILogger<CreateConfirmedExpenseHandler> logger
    )
    {
        _repo = repo;
        _categories = categories;
        _accounts = accounts;
        _businessPartners = businessPartners;
        _roles = roles;
        _paymentTerms = paymentTerms;
        _tax = tax;
        _payables = payables;
        _workflowPolicy = workflowPolicy;
        _retentionIssuer = retentionIssuer;
        _tenant = tenant;
        _company = company;
        _branch = branch;
        _user = user;
        _logger = logger;
    }

    public async Task<Result<ExpenseDocumentDetailDto>> Handle(
        CreateConfirmedExpenseCommand cmd,
        CancellationToken ct
    )
    {
        DocumentFlowPolicyResult policy;
        try
        {
            await _workflowPolicy.EnsureDirectCreationAllowedAsync(
                _company.CompanyId,
                DocTypeCodes.ExpenseDocument,
                ct
            );
            policy = await _workflowPolicy.GetRequiredAsync(_company.CompanyId, DocTypeCodes.ExpenseDocument, ct);
        }
        catch (DocumentFlowPolicyViolationException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(
                ExpenseWorkflowPolicyMessages.Translate(ex)
            );
        }

        var supplier = await ExpenseDraftRules.ResolveSupplierAsync(
            _businessPartners,
            _roles,
            _tenant.TenantId,
            cmd.SupplierId,
            ct
        );
        if (supplier.Error is not null)
            return supplier.Error.ToResult<ExpenseDocumentDetailDto>();

        var paymentTerm = await ExpenseDraftRules.ResolvePaymentTermAsync(
            _paymentTerms,
            _tenant.TenantId,
            cmd.PaymentTermId,
            supplier.Role,
            ct
        );
        if (paymentTerm.Error is not null)
            return paymentTerm.Error.ToResult<ExpenseDocumentDetailDto>();

        var duplicate = await _repo.GetBySupplierAndDocumentNumberAsync(
            _tenant.TenantId,
            cmd.SupplierId,
            cmd.DocumentType.Trim(),
            cmd.DocumentNumber.Trim(),
            ct
        );
        if (duplicate is not null)
            return Result<ExpenseDocumentDetailDto>.Conflict(
                "Ya existe un gasto registrado para este proveedor, tipo y numero de documento."
            );

        var dueDate = ExpenseDraftRules.ResolveDueDate(
            cmd.IssueDate,
            cmd.DueDate,
            paymentTerm.PaymentTerm!
        );
        if (dueDate.Error is not null)
            return dueDate.Error.ToResult<ExpenseDocumentDetailDto>();

        ExpenseDocument document;
        try
        {
            document = ExpenseDocument.CreateDraft(
                _tenant.TenantId,
                _company.CompanyId,
                _branch.BranchId,
                cmd.SupplierId,
                supplier.BusinessPartner!.Name.LegalName,
                supplier.BusinessPartner.Identification.Number,
                cmd.IssueDate,
                cmd.AccountingDate,
                cmd.DocumentType,
                cmd.DocumentNumber,
                paymentTerm.PaymentTerm!.Id,
                paymentTerm.PaymentTerm.Name,
                paymentTerm.PaymentTerm.Installments,
                paymentTerm.PaymentTerm.DaysBetweenInstallments,
                _user.UserId,
                cmd.AuthorizationNumber,
                cmd.AuthorizationDate,
                dueDate.Value,
                cmd.Notes
            );

            var lines = await ExpenseDraftRules.BuildLinesAsync(
                _categories,
                _accounts,
                _tax,
                _tenant.TenantId,
                _company.CompanyId,
                document.Id,
                cmd.Lines,
                ct
            );
            if (lines.Error is not null)
                return lines.Error.ToResult<ExpenseDocumentDetailDto>();

            document.ReplaceLines(lines.Lines!, _user.UserId);

            // Las líneas se acaban de construir con datos vigentes (categoría/cuenta) — a
            // diferencia de ConfirmExpenseDocumentHandler (que confirma un borrador que pudo
            // quedar desactualizado), no hace falta re-resolver contra ExpenseConfirmRules: el
            // snapshot que cada línea ya trae de ExpenseDraftRules.BuildLinesAsync es el mismo dato
            // fresco que Confirm() espera.
            var snapshots = document.Lines.ToDictionary(
                l => l.Id,
                l => (
                    l.SnapshotAccountingAccountId,
                    (string?)l.SnapshotAccountingAccountCode,
                    (string?)l.SnapshotAccountingAccountName
                )
            );
            document.Confirm(snapshots, _user.UserId);

            await _repo.AddAsync(document, ct);
        }
        catch (ArgumentException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
        }

        // RETENTIONS-EXPENSES-INTEGRATION-01D-1/01D-2: mismo criterio y mismos comentarios que
        // ConfirmExpenseDocumentHandler — se emite ANTES del SaveChangesAsync de abajo (staging vía
        // IRetentionIssuer, sin SaveChanges propio) para que ExpenseDocument (recién agregado
        // arriba), AccountsPayable (staged, con la retención ya aplicada) y RetentionDocument se
        // persistan atómicamente en un único SaveChangesAsync. Si falla cualquier paso, se retorna
        // sin persistir nada (ni el ExpenseDocument recién creado en memoria).
        var retentionAppliedToPayable = false;
        if (cmd.Retention is { AppliesRetention: true } retention)
        {
            var retentionResult = await _retentionIssuer.IssueForExpenseAsync(
                document,
                new RetentionIssueRequest(
                    _tenant.TenantId,
                    _company.CompanyId,
                    document.BranchId,
                    _user.UserId,
                    retention.EmissionPointId!.Value,
                    retention.RetentionNumber!,
                    retention.IssueDate!.Value,
                    retention.Lines!
                ),
                ct
            );
            if (!retentionResult.IsSuccess)
                return retentionResult.ToFailure<RetentionDocument, ExpenseDocumentDetailDto>();

            var retentionDocument = retentionResult.Value!;

            // Mismo criterio que ConfirmExpenseDocumentHandler: solo si la política declara
            // PayableGenerationMode.OnConfirmation hay una CxP a la que aplicar la retención en
            // esta misma operación.
            if (policy.PayableGenerationMode == PayableGenerationMode.OnConfirmation)
            {
                try
                {
                    var payable = await _payables.StageFromOriginAsync(
                        new CreateAccountsPayableFromOriginRequest(
                            _tenant.TenantId,
                            _company.CompanyId,
                            document.BranchId,
                            document.SupplierId,
                            AccountsPayableOriginType.ExpenseDocument,
                            document.Id,
                            document.DocumentType,
                            document.DocumentNumber,
                            document.IssueDate,
                            document.AccountingDate,
                            new[]
                            {
                                new AccountsPayableInstallmentInput(
                                    1,
                                    document.DueDate ?? document.AccountingDate,
                                    document.GrandTotal
                                ),
                            }
                        ),
                        _user.UserId,
                        ct
                    );
                    payable.ApplyRetention(retentionDocument.TotalRetained, _user.UserId);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    // Mismo criterio que ConfirmExpenseDocumentHandler: aquí SÍ debe fallar toda la
                    // operación — el usuario pidió explícitamente retención, nunca un estado
                    // intermedio con CxP bruta cuando se pidió neta.
                    return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
                }

                retentionAppliedToPayable = true;
            }
        }

        try
        {
            // Mismo criterio que ConfirmExpenseDocumentHandler (EXPENSES-CONFIRM-07): posting de
            // Gastos es estricto — ExpensePostingFailedException aborta la transacción completa,
            // nada de lo construido arriba llega a persistirse. RETENTIONS-EXPENSES-INTEGRATION-01D-2:
            // mismo criterio para RetentionPostingFailedException (posting de la retención).
            await _repo.SaveChangesAsync(ct);
        }
        catch (ExpensePostingFailedException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message, ex.Code);
        }
        catch (RetentionPostingFailedException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message, ex.Code);
        }

        // PAYABLES-GENERIC-FOUNDATION-09 + DOCUMENT-FLOW-POLICY-01, mismo criterio que
        // ConfirmExpenseDocumentHandler: solo si PayableGenerationMode.OnConfirmation Y no se creó
        // ya de forma staged por el camino de retención de arriba (evita un AccountsPayable
        // duplicado). El posting ya se confirmó y persistió arriba; un fallo aquí no revierte la
        // confirmación ya persistida — se registra para seguimiento manual. CreateFromOriginAsync es
        // idempotente.
        if (policy.PayableGenerationMode == PayableGenerationMode.OnConfirmation && !retentionAppliedToPayable)
        {
            try
            {
                await _payables.CreateFromOriginAsync(
                    new CreateAccountsPayableFromOriginRequest(
                        _tenant.TenantId,
                        _company.CompanyId,
                        document.BranchId,
                        document.SupplierId,
                        AccountsPayableOriginType.ExpenseDocument,
                        document.Id,
                        document.DocumentType,
                        document.DocumentNumber,
                        document.IssueDate,
                        document.AccountingDate,
                        new[]
                        {
                            new AccountsPayableInstallmentInput(
                                1,
                                document.DueDate ?? document.AccountingDate,
                                document.GrandTotal
                            ),
                        }
                    ),
                    _user.UserId,
                    ct
                );
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo crear la cuenta por pagar para el gasto {ExpenseDocumentId} ({DocumentNumber}) tras confirmar.",
                    document.Id,
                    document.DocumentNumber
                );
            }
        }

        return Result<ExpenseDocumentDetailDto>.Success(ExpenseDocumentMapper.ToDetail(document));
    }
}

file sealed record ExpenseConfirmError(string Message, string Code)
{
    public Result<T> ToResult<T>() =>
        Code == ApiResponseCodes.Common.NotFound
            ? Result<T>.NotFound(Message)
            : Result<T>.ValidationFailure(Message);
}

file sealed record LineAccountResolution(Account? Account, ExpenseConfirmError? Error);

/// <summary>
/// EXPENSES-CONFIRM-07 — re-valida, al confirmar, exactamente las mismas reglas que
/// <c>ExpenseDraftRules.BuildLinesAsync</c> exige al crear/editar el borrador (subcategoría activa,
/// nivel Subcategory, misma empresa, cuenta contable activa/postable/tipo Gasto/misma empresa).
/// Se revalida aquí porque ambas pudieron cambiar entre la creación del borrador y la confirmación
/// (la subcategoría puede desactivarse o cambiar de cuenta; la cuenta puede desactivarse o perder
/// AllowsPosting) — el snapshot tomado al crear la línea puede estar obsoleto.
/// </summary>
file static class ExpenseConfirmRules
{
    public static async Task<LineAccountResolution> ResolveLineAccountAsync(
        IExpenseCategoryRepository categories,
        IAccountRepository accounts,
        Guid tenantId,
        Guid companyId,
        ExpenseLine line,
        CancellationToken ct
    )
    {
        if (line.ExpenseSubcategoryId == Guid.Empty)
            return new LineAccountResolution(
                null,
                Validation("Cada linea debe tener una subcategoria de gasto.")
            );

        var category = await categories.GetByIdAsync(tenantId, line.ExpenseSubcategoryId, ct);
        if (category is null || category.CompanyId != companyId)
            return new LineAccountResolution(
                null,
                new ExpenseConfirmError(
                    "La subcategoria de gasto no existe.",
                    ApiResponseCodes.Common.NotFound
                )
            );
        if (!category.IsActive)
            return new LineAccountResolution(
                null,
                Validation($"La subcategoria '{category.Name}' esta inactiva.")
            );
        if (category.Level != ExpenseCategoryNodeLevel.Subcategory)
            return new LineAccountResolution(
                null,
                Validation("Cada linea debe apuntar a una subcategoria de gasto.")
            );
        if (!category.AccountingAccountId.HasValue)
            return new LineAccountResolution(
                null,
                Validation(
                    $"La subcategoria '{category.Name}' no tiene cuenta contable configurada."
                )
            );

        var account = await accounts.GetByIdAsync(
            tenantId,
            companyId,
            category.AccountingAccountId.Value,
            ct
        );
        if (account is null)
            return new LineAccountResolution(
                null,
                new ExpenseConfirmError(
                    $"La cuenta contable de la subcategoria '{category.Name}' no existe.",
                    ApiResponseCodes.Common.NotFound
                )
            );
        if (!account.IsActive)
            return new LineAccountResolution(
                null,
                Validation($"La cuenta contable de la subcategoria '{category.Name}' esta inactiva.")
            );
        if (!account.AllowsPosting)
            return new LineAccountResolution(
                null,
                Validation(
                    $"La cuenta contable de la subcategoria '{category.Name}' no permite contabilizacion."
                )
            );
        if (account.AccountType != AccountType.Expense)
            return new LineAccountResolution(
                null,
                Validation($"La cuenta contable de la subcategoria '{category.Name}' debe ser de tipo gasto.")
            );

        return new LineAccountResolution(account, null);
    }

    private static ExpenseConfirmError Validation(string message) =>
        new(message, ApiResponseCodes.Common.ValidationError);
}
