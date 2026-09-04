using ERP.Application.Common;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Constants;

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

/// <summary>
/// Datos ya resueltos del contexto seguro + intención de retención, para <see cref="IRetentionIssuer"/>.
/// RETENTIONS-DOCUMENT-SEQUENCE-02E: ya NO incluye un número de retención manual — se genera
/// internamente vía <see cref="IDocumentSequenceRepository.CaptureNextAsync"/> a partir de
/// <see cref="EmissionPointId"/>, que este record ya traía desde antes de esta fase.
/// </summary>
public sealed record RetentionIssueRequest(
    Guid TenantId,
    Guid CompanyId,
    Guid BranchId,
    Guid UserId,
    Guid EmissionPointId,
    DateOnly IssueDate,
    IReadOnlyList<IssueRetentionLineInput> Lines
);

public sealed class RetentionIssuer : IRetentionIssuer
{
    /// <summary>Código SRI "07" = Comprobante de Retención — misma identidad fija que
    /// <c>IssueWithholdingHandler.WithholdingDocTypeCode</c>, la retención de Compras ya emitida
    /// hoy vía la misma infraestructura central de secuencias.</summary>
    private const string RetentionDocTypeCode = SriDocumentTypeCodes.Withholding;

    private readonly IRetentionDocumentRepository _retentionRepo;
    private readonly IRetentionEligibilityService _eligibilityService;
    private readonly IEmissionPointRepository _emissionPointRepo;
    private readonly IEstablishmentRepository _establishmentRepo;
    private readonly IDocumentSequenceRepository _sequenceRepo;

    public RetentionIssuer(
        IRetentionDocumentRepository retentionRepo,
        IRetentionEligibilityService eligibilityService,
        IEmissionPointRepository emissionPointRepo,
        IEstablishmentRepository establishmentRepo,
        IDocumentSequenceRepository sequenceRepo
    )
    {
        _retentionRepo = retentionRepo;
        _eligibilityService = eligibilityService;
        _emissionPointRepo = emissionPointRepo;
        _establishmentRepo = establishmentRepo;
        _sequenceRepo = sequenceRepo;
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

        // RETENTIONS-DOCUMENT-SEQUENCE-02E — resolver el punto de emisión y su establecimiento
        // ANTES de construir el agregado (mismo orden que IssueWithholdingHandler): valida que
        // exista y pertenezca a la empresa/tenant activos (GetByIdAsync ya filtra por tenant +
        // query filter global de empresa — un punto de emisión de otro tenant/empresa nunca es
        // visible aquí) antes de gastar ningún recurso construyendo líneas.
        var emissionPoint = await _emissionPointRepo.GetByIdAsync(
            request.EmissionPointId,
            request.TenantId,
            ct
        );
        if (emissionPoint is null)
            return Result<RetentionDocument>.NotFound("Punto de emisión no encontrado.");

        var establishment = await _establishmentRepo.GetByIdAsync(
            request.TenantId,
            emissionPoint.EstablishmentId,
            ct
        );
        if (establishment is null)
            return Result<RetentionDocument>.NotFound("Establecimiento no encontrado.");

        RetentionDocument retention;
        try
        {
            // RETENTIONS-TAX-COMPONENT-MODEL-02B — snapshot del documento sustento, resuelto AQUÍ
            // (Application, con el ExpenseDocument ya cargado) y nunca por el propio agregado.
            // TaxSupportCode (codSustento) queda null: ExpenseDocument no lo captura hoy (solo
            // PurchaseInvoice.TaxSupportCode lo hace) y SupplierRoleConfig.DefaultTaxSupportCode no
            // está cargado en este flujo — gap documentado, no un olvido (ver comentario del campo
            // en RetentionDocument.SourceDocumentTaxSupportCode).
            var sourceSnapshot = new RetentionDocument.SourceDocumentSnapshot(
                document.DocumentType,
                document.DocumentNumber,
                document.IssueDate,
                document.AuthorizationNumber,
                null,
                document.Subtotal,
                document.GrandTotal
            );

            retention = RetentionDocument.Create(
                request.TenantId,
                request.CompanyId,
                request.BranchId,
                RetentionSourceDocumentType.ExpenseDocument,
                document.Id,
                document.SupplierId,
                request.EmissionPointId,
                request.UserId,
                sourceSnapshot
            );

            foreach (var line in request.Lines)
            {
                retention.AddLine(
                    RetentionDocumentLine.Create(
                        retention.Id,
                        request.TenantId,
                        line.TaxType,
                        line.RetentionCode,
                        // RETENTIONS-TAX-COMPONENT-MODEL-02B: RetentionCodeDescription es requerido
                        // a nivel de dominio, pero opcional en el contrato de entrada
                        // (IssueRetentionLineInput) para no romper el frontend actual (que no
                        // captura este dato todavía — no hay selector de catálogo real en UI). Si
                        // no llega, se usa el propio código como descripción de respaldo — límite
                        // temporal documentado, a mejorar cuando exista ese selector.
                        line.RetentionCodeDescription is { Length: > 0 }
                            ? line.RetentionCodeDescription
                            : line.RetentionCode,
                        line.BaseAmount,
                        line.RetentionRate,
                        line.RetainedAmount,
                        line.Description
                    )
                );
            }

            // CaptureNextAsync: atómico (advisory lock + transacción propia) — mismo punto de
            // entrada FROZEN (ADR-019) que ya usa IssueWithholdingHandler para el mismo doc type
            // "07". Se llama aquí, lo más tarde posible (líneas ya construidas, justo antes de
            // Issue()), para minimizar la ventana de un hueco si algo falla después. El número
            // nunca llega desde el cliente — RetentionIssueRequest ya no tiene ese campo.
            var sequential = await _sequenceRepo.CaptureNextAsync(
                request.TenantId,
                request.CompanyId,
                request.EmissionPointId,
                RetentionDocTypeCode,
                ct
            );
            var retentionNumber = $"{establishment.Code}-{emissionPoint.Code}-{sequential}";

            retention.Issue(retentionNumber, request.IssueDate, request.UserId);
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
