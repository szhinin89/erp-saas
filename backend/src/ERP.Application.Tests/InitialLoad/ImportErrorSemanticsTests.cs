using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.UploadImportFile;
using ERP.Application.Modules.InitialLoad.UseCases.ValidateImportBatch;
using ERP.Application.Tests.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.InitialLoad;

/// <summary>
/// ZH-DOMAIN-RULE-ERROR-SSOT-01 — los importadores convierten una regla de negocio en archivo/fila
/// inválida desde el error semántico oficial (<see cref="DomainRuleViolationException"/>, mensaje
/// público) y nunca desde el texto de una excepción técnica (que queda solo en el log).
/// </summary>
public sealed class ImportErrorSemanticsTests
{
    private const string TechnicalDetail = "Npgsql: Host=db.internal · ClosedXML stack detail";
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IImportBatchRepository> Batches { get; } = new();
        public Mock<IImportBatchRowRepository> Rows { get; } = new();
        public Mock<IImportBatchIssueRepository> Issues { get; } = new();
        public Mock<IFileStorage> Files { get; } = new();
        public Mock<IImportProcessor> Processor { get; } = new();
        public Mock<IOperationalContext> Ctx { get; } = new();
        public List<ImportBatchIssue> RecordedIssues { get; } = [];

        public Fixture(ImportBatch batch)
        {
            Ctx.Setup(c => c.TenantId).Returns(Tenant);
            Ctx.Setup(c => c.CompanyId).Returns(Company);
            Ctx.Setup(c => c.UserId).Returns(User);
            Processor.Setup(p => p.ImportType).Returns(ImportType.Suppliers);
            Batches
                .Setup(b =>
                    b.GetByIdAsync(batch.Id, Tenant, Company, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(batch);
            Files
                .Setup(f => f.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream([1, 2, 3]));
            Issues
                .Setup(i => i.AddAsync(It.IsAny<ImportBatchIssue>(), It.IsAny<CancellationToken>()))
                .Callback<ImportBatchIssue, CancellationToken>(
                    (issue, _) => RecordedIssues.Add(issue)
                )
                .Returns(Task.CompletedTask);
        }

        private Dictionary<ImportType, IImportProcessor> ProcessorMap =>
            new() { [ImportType.Suppliers] = Processor.Object };

        public ValidateImportBatchHandler Validate() =>
            new(
                Batches.Object,
                Rows.Object,
                Issues.Object,
                Files.Object,
                ProcessorMap,
                Ctx.Object,
                NullLogger<ValidateImportBatchHandler>.Instance, Mock.Of<IUnitOfWork>()
            );

        public ConfirmImportBatchHandler Confirm() =>
            new(
                Batches.Object,
                Rows.Object,
                Issues.Object,
                ProcessorMap,
                Ctx.Object,
                NullLogger<ConfirmImportBatchHandler>.Instance,
                Mock.Of<IUnitOfWork>()
            );
    }

    private static ImportBatch UploadedBatch()
    {
        var batch = ImportBatch.Create(Tenant, Company, ImportType.Suppliers, User);
        batch.AttachFile("initial-load/f.xlsx", "f.xlsx", 3, User);
        batch.MarkUploaded(User);
        return batch;
    }

    private static ImportBatch ValidatedBatch()
    {
        var batch = UploadedBatch();
        batch.BeginValidating(User);
        batch.CompleteValidation(1, 1, 0, 0, User);
        return batch;
    }

    // ── Validación del archivo ──

    [Fact]
    public async Task Archivo_invalido_regla_publica_del_lector_reporta_su_motivo()
    {
        var batch = UploadedBatch();
        var f = new Fixture(batch);
        f.Processor.Setup(p => p.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new DomainRuleViolationException(
                    "El archivo no es un Excel (.xlsx) válido.",
                    new InvalidOperationException(TechnicalDetail)
                )
            );

        var result = await f.Validate()
            .Handle(new ValidateImportBatchCommand(batch.Id), CancellationToken.None);

        result
            .Error.Should()
            .Be("No se pudo leer el archivo: El archivo no es un Excel (.xlsx) válido.");
        batch.Status.Should().Be(ImportStatus.Failed);
    }

    [Fact]
    public async Task Error_tecnico_al_leer_no_expone_el_detalle()
    {
        var batch = UploadedBatch();
        var f = new Fixture(batch);
        f.Processor.Setup(p => p.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(TechnicalDetail));

        var result = await f.Validate()
            .Handle(new ValidateImportBatchCommand(batch.Id), CancellationToken.None);

        result.Error.Should().Be("No se pudo leer el archivo.");
        batch.Status.Should().Be(ImportStatus.Failed);
    }

    // ── Confirmación por fila ──

    private static async Task<ImportBatchIssue?> ConfirmOneRow(Action<Mock<IImportProcessor>> setup)
    {
        var batch = ValidatedBatch();
        var f = new Fixture(batch);
        var row = ImportBatchRow.Create(Tenant, Company, batch.Id, 1, "{}", User);
        row.SetParsedData("{}", hasBlockingIssue: false, User);
        f.Rows.SetupSequence(r =>
                r.GetValidRowsPageAsync(
                    batch.Id,
                    Tenant,
                    Company,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([row])
            .ReturnsAsync([]);
        setup(f.Processor);

        await f.Confirm().Handle(new ConfirmImportBatchCommand(batch.Id), CancellationToken.None);

        return f.RecordedIssues.SingleOrDefault();
    }

    [Fact]
    public async Task Fila_rechazada_por_regla_de_negocio_reporta_el_mensaje_publico()
    {
        var issue = await ConfirmOneRow(p =>
            p.Setup(x => x.ConfirmRowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(
                    new DomainRuleViolationException("El BusinessPartner ya está inactivo.")
                )
        );

        issue!.Code.Should().Be("CONFIRM_FAILED");
        issue.Message.Should().Be("El BusinessPartner ya está inactivo.");
    }

    [Fact]
    public async Task Fila_con_error_tecnico_se_reporta_invalida_sin_detalle_interno()
    {
        var issue = await ConfirmOneRow(p =>
            p.Setup(x => x.ConfirmRowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException(TechnicalDetail))
        );

        issue!.Code.Should().Be("CONFIRM_FAILED");
        issue
            .Message.Should()
            .NotContain("Npgsql")
            .And.NotContain("db.internal")
            .And.Contain("Error interno");
    }

    [Fact]
    public async Task Fila_con_fallo_esperado_del_procesador_conserva_su_comportamiento()
    {
        var issue = await ConfirmOneRow(p =>
            p.Setup(x => x.ConfirmRowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(RowConfirmResult.Failed("Identificación duplicada."))
        );

        issue!.Message.Should().Be("Identificación duplicada.");
    }

    // ── 01B: atomicidad de la carga del archivo ──

    [Fact]
    public async Task Regla_que_rechaza_el_adjunto_compensa_el_archivo_ya_guardado()
    {
        // Lote ya validado: el dominio no admite adjuntar otro archivo (regla), pero el archivo ya se
        // escribió en el almacenamiento — antes quedaba huérfano.
        var batch = ValidatedBatch();
        var f = new Fixture(batch);
        f.Files.Setup(x =>
                x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync("initial-load/stored.xlsx");
        var handler = new UploadImportFileHandler(f.Batches.Object, f.Files.Object, f.Ctx.Object);
        var content = new MediaUploadContent(
            new MemoryStream([1, 2, 3]),
            "f.xlsx",
            "application/vnd.ms-excel",
            3
        );

        var result = await handler.HandleWithDomainRules(
            new UploadImportFileCommand(batch.Id, content),
            CancellationToken.None
        );

        result.Code.Should().Be(ApiResponseCodes.Common.DomainRuleViolation);
        f.Files.Verify(
            x => x.DeleteAsync("initial-load/stored.xlsx", It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.Batches.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
