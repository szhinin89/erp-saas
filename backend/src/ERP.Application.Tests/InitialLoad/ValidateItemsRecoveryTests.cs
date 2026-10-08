using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.UseCases.ValidateImportBatch;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.InitialLoad;

public sealed class ValidateItemsRecoveryTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("missing")]
    [InlineData("cancel")]
    [InlineData("failure")]
    public async Task Reemplazo_de_staging_solo_se_confirma_si_validacion_completa(string kind)
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var user = Guid.NewGuid();
        var batch = ImportBatch.Create(tenant, company, ImportType.Items, user);
        batch.AttachFile("test.xlsx", "test.xlsx", 1, user);
        batch.MarkUploaded(user);
        batch.BeginValidating(user);
        batch.CompleteValidation(1, 1, 0, 0, user);
        var batches = new Mock<IImportBatchRepository>();
        batches.Setup(x => x.GetByIdAsync(batch.Id, tenant, company, It.IsAny<CancellationToken>())).ReturnsAsync(batch);
        batches.Setup(x => x.GetByIdForUpdateAsync(batch.Id, tenant, company, It.IsAny<CancellationToken>())).ReturnsAsync(batch);
        var rows = new Mock<IImportBatchRowRepository>();
        var issues = new Mock<IImportBatchIssueRepository>();
        var files = new Mock<IFileStorage>();
        files.Setup(x => x.GetAsync("test.xlsx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(kind == "missing" ? null : new MemoryStream([1]));
        var processor = new Mock<IImportProcessor>();
        processor.Setup(x => x.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportReadResult([new Dictionary<string, string?>()]));
        var validation = processor.Setup(x => x.ValidateRowAsync(1, It.IsAny<IReadOnlyDictionary<string, string?>>(), false, It.IsAny<CancellationToken>()));
        if (kind == "cancel") validation.ThrowsAsync(new OperationCanceledException());
        else if (kind == "failure") validation.ThrowsAsync(new InvalidOperationException("Injected"));
        else validation.ReturnsAsync(new RowValidationResult("{}", false, []));
        var uow = new Mock<IUnitOfWork>();
        var ctx = Mock.Of<IOperationalContext>(x => x.TenantId == tenant && x.CompanyId == company && x.UserId == user);
        var handler = new ValidateImportBatchHandler(batches.Object, rows.Object, issues.Object, files.Object,
            new Dictionary<ImportType, IImportProcessor> { [ImportType.Items] = processor.Object }, ctx,
            NullLogger<ValidateImportBatchHandler>.Instance, uow.Object);
        if (kind == "cancel")
            await FluentActions.Awaiting(() => handler.Handle(new(batch.Id), default)).Should().ThrowAsync<OperationCanceledException>();
        else
            (await handler.Handle(new(batch.Id), default)).IsSuccess.Should().Be(kind == "success");
        rows.Verify(x => x.DeleteByBatchAsync(batch.Id, tenant, company, It.IsAny<CancellationToken>()), Times.Once);
        issues.Verify(x => x.DeleteByBatchAsync(batch.Id, tenant, company, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), kind == "success" ? Times.Once : Times.Never);
        uow.Verify(x => x.RollbackAsync(CancellationToken.None), kind == "success" ? Times.Never : Times.Once);
        uow.Verify(x => x.ClearChangeTracker(), kind == "success" ? Times.Never : Times.Once);
    }
}
