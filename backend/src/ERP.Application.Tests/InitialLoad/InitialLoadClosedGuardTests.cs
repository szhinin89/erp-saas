using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Modules.InitialLoad;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.UseCases.CreateImportBatch;
using ERP.Application.Modules.InitialLoad.UseCases.UploadImportFile;
using ERP.Application.Modules.InitialLoad.UseCases.ValidateImportBatch;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Application.Tests.InitialLoad;

/// <summary>
/// IL-8E — los motores genéricos de importación consultan el cierre definitivo de la Carga Inicial
/// solo para lotes de saldos (<see cref="IOpeningBalanceImport"/>): crear, subir y validar se
/// rechazan sin escribir nada; los catálogos (Productos/Clientes/Proveedores) siguen permitidos.
/// La confirmación (bajo bloqueo empresa → lote) se cubre en PostgreSQL.
/// </summary>
public sealed class InitialLoadClosedGuardTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    private readonly Mock<IImportBatchRepository> _batches = new();
    private readonly Mock<IFileStorage> _files = new();
    private readonly Mock<IOperationalContext> _ctx = new();

    public InitialLoadClosedGuardTests()
    {
        _ctx.SetupGet(c => c.TenantId).Returns(Tenant);
        _ctx.SetupGet(c => c.CompanyId).Returns(Company);
        _ctx.SetupGet(c => c.UserId).Returns(User);
        _files.Setup(f => f.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("initial-load/stored.xlsx");
    }

    /// <summary>Procesador de saldos con la Carga Inicial cerrada.</summary>
    private static IImportProcessor ClosedBalanceProcessor(ImportType type)
    {
        var processor = new Mock<IImportProcessor>();
        processor.SetupGet(p => p.ImportType).Returns(type);
        processor.As<IOpeningBalanceImport>()
            .Setup(p => p.CheckInitialLoadOpenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyEntity.InitialLoadClosedMessage);
        return processor.Object;
    }

    /// <summary>Procesador de catálogo: no implementa <see cref="IOpeningBalanceImport"/>.</summary>
    private static IImportProcessor CatalogProcessor(ImportType type) =>
        Mock.Of<IImportProcessor>(p => p.ImportType == type);

    private static Dictionary<ImportType, IImportProcessor> Processors(IImportProcessor processor) =>
        new() { [processor.ImportType] = processor };

    private ImportBatch StoredBatch(ImportType type, bool uploaded)
    {
        var batch = ImportBatch.Create(Tenant, Company, type, User);
        if (uploaded)
        {
            batch.AttachFile("x.xlsx", "x.xlsx", 1, User);
            batch.MarkUploaded(User);
        }
        _batches.Setup(b => b.GetByIdAsync(batch.Id, Tenant, Company, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);
        return batch;
    }

    private static UploadImportFileCommand Upload(Guid batch) =>
        new(batch, new MediaUploadContent(new MemoryStream([1, 2, 3]), "f.xlsx", "application/vnd.ms-excel", 3));

    private static void ShouldBeClosed<T>(Result<T> result)
    {
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(InitialLoadClosedGuard.Code);
        result.Error.Should().Be(CompanyEntity.InitialLoadClosedMessage);
    }

    [Theory]
    [InlineData(ImportType.InitialStock)]
    [InlineData(ImportType.InitialReceivables)]
    [InlineData(ImportType.InitialPayables)]
    public async Task Crear_lote_de_saldos_tras_el_cierre_se_rechaza_sin_crear_nada(ImportType type)
    {
        var handler = new CreateImportBatchHandler(_batches.Object, Processors(ClosedBalanceProcessor(type)), _ctx.Object);

        ShouldBeClosed(await handler.Handle(new CreateImportBatchCommand(type), CancellationToken.None));
        _batches.Verify(b => b.AddAsync(It.IsAny<ImportBatch>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ImportType.Items)]
    [InlineData(ImportType.Customers)]
    [InlineData(ImportType.Suppliers)]
    public async Task Catalogos_se_siguen_creando_y_subiendo(ImportType type)
    {
        var processors = Processors(CatalogProcessor(type));
        var created = await new CreateImportBatchHandler(_batches.Object, processors, _ctx.Object)
            .Handle(new CreateImportBatchCommand(type), CancellationToken.None);
        created.IsSuccess.Should().BeTrue(created.Error);

        var batch = StoredBatch(type, uploaded: false);
        var uploaded = await new UploadImportFileHandler(_batches.Object, _files.Object, _ctx.Object, processors)
            .Handle(Upload(batch.Id), CancellationToken.None);
        uploaded.IsSuccess.Should().BeTrue(uploaded.Error);
        batch.Status.Should().Be(ImportStatus.Uploaded);
    }

    [Fact]
    public async Task Subir_archivo_a_lote_de_saldos_tras_el_cierre_se_rechaza_sin_guardar_el_archivo()
    {
        var batch = StoredBatch(ImportType.InitialPayables, uploaded: false);
        var handler = new UploadImportFileHandler(_batches.Object, _files.Object, _ctx.Object,
            Processors(ClosedBalanceProcessor(ImportType.InitialPayables)));

        ShouldBeClosed(await handler.Handle(Upload(batch.Id), CancellationToken.None));
        _files.Verify(f => f.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        batch.Status.Should().Be(ImportStatus.Draft);
    }

    [Fact]
    public async Task Validar_lote_de_saldos_tras_el_cierre_se_rechaza_sin_cambiar_estado()
    {
        var batch = StoredBatch(ImportType.InitialStock, uploaded: true);
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new ValidateImportBatchHandler(_batches.Object, Mock.Of<IImportBatchRowRepository>(),
            Mock.Of<IImportBatchIssueRepository>(), _files.Object, Processors(ClosedBalanceProcessor(ImportType.InitialStock)),
            _ctx.Object, NullLogger<ValidateImportBatchHandler>.Instance, unitOfWork.Object);

        ShouldBeClosed(await handler.Handle(new ValidateImportBatchCommand(batch.Id), CancellationToken.None));
        batch.Status.Should().Be(ImportStatus.Uploaded);
        unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
