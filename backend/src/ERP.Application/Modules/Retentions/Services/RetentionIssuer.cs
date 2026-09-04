using ERP.Application.Common;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;

namespace ERP.Application.Modules.Retentions.Services;

/// <summary>
/// RETENTIONS-EXPENSES-INTEGRATION-01D-1 — operación interna reutilizable que construye y emite un
/// <see cref="RetentionDocument"/> sobre un <see cref="ExpenseDocument"/> YA CARGADO por el
/// llamador (no lo vuelve a consultar ni revalida su estado — esa responsabilidad es de quien
/// orquesta: <c>IssueRetentionHandler</c> para la emisión aislada post-confirmación,
/// <c>ConfirmExpenseDocumentHandler</c>/<c>CreateConfirmedExpenseHandler</c> para la emisión
/// integrada en la confirmación transaccional).
///
/// Deliberadamente NO llama <c>SaveChangesAsync</c>/<c>IUnitOfWork</c> — solo
/// <see cref="IRetentionDocumentRepository.AddAsync"/> (staging). Quien invoca esta operación decide
/// cuándo persistir, para poder incluir la retención en su propia transacción/SaveChanges (evita el
/// doble SaveChanges que rompería la atomicidad de la confirmación de gastos — ver
/// <c>docs/decisions/RETENTIONS-MODULE-DESIGN-01.md</c> § "Flujo desde Gastos").
///
/// No sobre-generaliza a otros orígenes (Compras) en esta fase — <see cref="IssueForExpenseAsync"/>
/// fija <c>SourceDocumentType.ExpenseDocument</c> deliberadamente.
/// </summary>
public interface IRetentionIssuer
{
    Task<Result<RetentionDocument>> IssueForExpenseAsync(
        ExpenseDocument document,
        RetentionIssueRequest request,
        CancellationToken ct = default
    );
}

/// <summary>Datos ya resueltos del contexto seguro + intención de retención, para <see cref="IRetentionIssuer"/>.</summary>
public sealed record RetentionIssueRequest(
    Guid TenantId,
    Guid CompanyId,
    Guid BranchId,
    Guid UserId,
    Guid EmissionPointId,
    string RetentionNumber,
    DateOnly IssueDate,
    IReadOnlyList<IssueRetentionLineInput> Lines
);

public sealed class RetentionIssuer : IRetentionIssuer
{
    private readonly IRetentionDocumentRepository _retentionRepo;
    private readonly IRetentionEligibilityService _eligibilityService;

    public RetentionIssuer(
        IRetentionDocumentRepository retentionRepo,
        IRetentionEligibilityService eligibilityService
    )
    {
        _retentionRepo = retentionRepo;
        _eligibilityService = eligibilityService;
    }

    public async Task<Result<RetentionDocument>> IssueForExpenseAsync(
        ExpenseDocument document,
        RetentionIssueRequest request,
        CancellationToken ct = default
    )
    {
        // Unicidad por origen — nunca crear una segunda retención activa sobre el mismo origen
        // (ver docs/decisions/RETENTIONS-MODULE-DESIGN-01.md § "Agregado raíz").
        var alreadyExists = await _retentionRepo.ExistsActiveBySourceAsync(
            request.TenantId,
            request.CompanyId,
            RetentionSourceDocumentType.ExpenseDocument,
            document.Id,
            ct
        );
        if (alreadyExists)
            return Result<RetentionDocument>.Conflict(
                "Ya existe una retención activa para este documento origen."
            );

        // Revalidar elegibilidad server-side con la base retenible real del ExpenseDocument — nunca
        // confía en las líneas que el usuario/caller envió como prueba de que aplica.
        var eligibility = await _eligibilityService.EvaluateAsync(
            request.TenantId,
            request.CompanyId,
            document.SupplierId,
            document.TotalVat,
            document.Lines.Sum(l => l.TaxableBase),
            ct
        );

        var wantsVat = request.Lines.Any(l => l.TaxType == RetentionTaxType.Vat);
        var wantsIncome = request.Lines.Any(l => l.TaxType == RetentionTaxType.Income);

        if (wantsVat && !eligibility.CanRetainVat)
            return Result<RetentionDocument>.ValidationFailure(string.Join(" ", eligibility.Reasons));
        if (wantsIncome && !eligibility.CanRetainIncome)
            return Result<RetentionDocument>.ValidationFailure(string.Join(" ", eligibility.Reasons));

        RetentionDocument retention;
        try
        {
            retention = RetentionDocument.Create(
                request.TenantId,
                request.CompanyId,
                request.BranchId,
                RetentionSourceDocumentType.ExpenseDocument,
                document.Id,
                document.SupplierId,
                request.EmissionPointId,
                request.UserId
            );

            foreach (var line in request.Lines)
            {
                retention.AddLine(
                    RetentionDocumentLine.Create(
                        retention.Id,
                        request.TenantId,
                        line.TaxType,
                        line.RetentionCode,
                        line.BaseAmount,
                        line.RetentionRate,
                        line.RetainedAmount,
                        line.Description
                    )
                );
            }

            retention.Issue(request.RetentionNumber, request.IssueDate, request.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<RetentionDocument>.ValidationFailure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<RetentionDocument>.ValidationFailure(ex.Message);
        }

        // NO SaveChangesAsync aquí — solo staging. El llamador decide cuándo persistir (ver
        // comentario de tipo de la interfaz).
        await _retentionRepo.AddAsync(retention, ct);

        return Result<RetentionDocument>.Success(retention);
    }
}
