using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.InitialLoad;

public sealed class ConfirmItemsAtomicTests
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _company = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Mock<IImportBatchRepository> _batches = new();
    private readonly Mock<IImportBatchRowRepository> _rows = new();
    private readonly Mock<IImportBatchIssueRepository> _issues = new();
    private readonly Mock<IImportProcessor> _processor = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private (ConfirmImportBatchHandler Handler, ImportBatch Batch, List<ImportBatchRow> Rows) Setup(bool auto = false, int errors = 0)
    {
        var batch = ImportBatch.Create(_tenant, _company, ImportType.Items, _user, autoCreateCatalogValues: auto);
        batch.AttachFile("test", "test.xlsx", 1, _user);
        batch.MarkUploaded(_user);
        batch.BeginValidating(_user);
        batch.CompleteValidation(2, 2 - errors, errors, 0, _user);
        var rows = Enumerable.Range(1, 2).Select(i => ImportBatchRow.Create(_tenant, _company, batch.Id, i, "{}", _user)).ToList();
        foreach (var row in rows) row.SetParsedData(row.RowNumber.ToString(), false, _user);
        _batches.Setup(x => x.GetByIdAsync(batch.Id, _tenant, _company, It.IsAny<CancellationToken>())).ReturnsAsync(batch);
        _batches.Setup(x => x.GetByIdForUpdateAsync(batch.Id, _tenant, _company, It.IsAny<CancellationToken>())).ReturnsAsync(batch);
        _rows.Setup(x => x.GetValidRowsPageAsync(batch.Id, _tenant, _company, 200, It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        var ctx = Mock.Of<IOperationalContext>(x => x.TenantId == _tenant && x.CompanyId == _company && x.UserId == _user);
        var handler = new ConfirmImportBatchHandler(_batches.Object, _rows.Object, _issues.Object,
            new Dictionary<ImportType, IImportProcessor> { [ImportType.Items] = _processor.Object }, ctx,
            NullLogger<ConfirmImportBatchHandler>.Instance, _uow.Object);
        return (handler, batch, rows);
    }

    [Fact]
    public async Task Retry_de_lote_completado_devuelve_resultado_sin_procesar_filas()
    {
        var catalog = _processor.As<ICatalogImportConfirmation>();
        var f = Setup();
        catalog.Setup(x => x.ConfirmRowAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => RowConfirmResult.Success(Guid.NewGuid()));
        var first = await f.Handler.Handle(new(f.Batch.Id), default);
        var retry = await f.Handler.Handle(new(f.Batch.Id), default);
        retry.IsSuccess.Should().BeTrue();
        retry.Value.Should().Be(first.Value);
        catalog.Verify(x => x.ConfirmRowAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _rows.Verify(x => x.GetValidRowsPageAsync(f.Batch.Id, _tenant, _company, 200, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Lote_con_errores_se_rechaza_bajo_bloqueo()
    {
        var f = Setup(errors: 1);
        var result = await f.Handler.Handle(new(f.Batch.Id), default);
        result.IsSuccess.Should().BeFalse();
        f.Batch.Status.Should().Be(ImportStatus.Validated);
        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _processor.Verify(x => x.ConfirmRowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Error_en_filas_se_rechaza_aunque_contador_sea_cero()
    {
        var f = Setup();
        _rows.Setup(x => x.GetPageAsync(f.Batch.Id, _tenant, _company, 1, 1, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Rows: (IReadOnlyList<ImportBatchRow>)[], TotalCount: 1));
        var result = await f.Handler.Handle(new(f.Batch.Id), default);
        result.IsSuccess.Should().BeFalse();
        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exito_confirma_todo_y_transmite_opcion_del_lote(bool auto)
    {
        var catalog = _processor.As<ICatalogImportConfirmation>();
        var f = Setup(auto);
        catalog.Setup(x => x.ConfirmRowAsync(It.IsAny<string>(), auto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => RowConfirmResult.Success(Guid.NewGuid()));
        var result = await f.Handler.Handle(new(f.Batch.Id), default);
        result.IsSuccess.Should().BeTrue();
        result.Value!.ImportedRows.Should().Be(2);
        result.Value.Status.Should().Be(ImportStatus.Completed);
        f.Rows.Should().OnlyContain(r => r.IsImported);
        catalog.Verify(x => x.ConfirmRowAsync(It.IsAny<string>(), auto, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _uow.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("result")]
    [InlineData("exception")]
    [InlineData("cancel")]
    public async Task Primer_fallo_aborta_sin_skip_ni_commit(string kind)
    {
        var catalog = _processor.As<ICatalogImportConfirmation>();
        var f = Setup();
        var setup = catalog.Setup(x => x.ConfirmRowAsync("1", false, It.IsAny<CancellationToken>()));
        if (kind == "result") setup.ReturnsAsync(RowConfirmResult.Failed("Rechazo"));
        else if (kind == "exception") setup.ThrowsAsync(new InvalidOperationException("Detalle técnico"));
        else setup.ThrowsAsync(new OperationCanceledException());
        if (kind == "cancel")
        {
            var act = () => f.Handler.Handle(new(f.Batch.Id), default);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        else
        {
            var result = await f.Handler.Handle(new(f.Batch.Id), default);
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().NotContain("Detalle técnico");
        }
        catalog.Verify(x => x.ConfirmRowAsync("2", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.RollbackAsync(CancellationToken.None), Times.Once);
        _uow.Verify(x => x.ClearChangeTracker(), Times.Once);
        _uow.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _issues.Verify(x => x.AddAsync(It.IsAny<ImportBatchIssue>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Fila_sin_parsed_no_se_omite()
    {
        _processor.As<ICatalogImportConfirmation>();
        var f = Setup();
        f.Rows[0] = ImportBatchRow.Create(_tenant, _company, f.Batch.Id, 1, "{}", _user);
        var result = await f.Handler.Handle(new(f.Batch.Id), default);
        result.IsSuccess.Should().BeFalse();
        _uow.Verify(x => x.RollbackAsync(CancellationToken.None), Times.Once);
    }
}
