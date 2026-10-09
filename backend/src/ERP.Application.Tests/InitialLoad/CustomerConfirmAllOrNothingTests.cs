using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.InitialLoad;

/// <summary>Clientes (IL-2A) y Proveedores (IL-3A) son todo-o-nada: una fila con error bloquea el lote.</summary>
public sealed class CustomerConfirmAllOrNothingTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    [Fact]
    public async Task Lote_de_clientes_con_una_fila_con_error_no_se_confirma()
    {
        var batch = ImportBatch.Create(Tenant, Company, ImportType.Customers, User);
        batch.AttachFile("initial-load/c.xlsx", "c.xlsx", 3, User);
        batch.MarkUploaded(User);
        batch.BeginValidating(User);
        batch.CompleteValidation(totalRows: 3, validRows: 2, issueRows: 1, warningRows: 0, User);

        var batches = new Mock<IImportBatchRepository>();
        batches.Setup(b => b.GetByIdAsync(batch.Id, Tenant, Company, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);
        batches.Setup(b => b.GetByIdForUpdateAsync(batch.Id, Tenant, Company, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);
        var rows = new Mock<IImportBatchRowRepository>();
        var processor = new Mock<IImportProcessor>();
        var ctx = new Mock<IOperationalContext>();
        ctx.Setup(c => c.TenantId).Returns(Tenant);
        ctx.Setup(c => c.CompanyId).Returns(Company);
        ctx.Setup(c => c.UserId).Returns(User);
        var handler = new ConfirmImportBatchHandler(batches.Object, rows.Object,
            Mock.Of<IImportBatchIssueRepository>(),
            new Dictionary<ImportType, IImportProcessor> { [ImportType.Customers] = processor.Object },
            ctx.Object, NullLogger<ConfirmImportBatchHandler>.Instance, Mock.Of<IUnitOfWork>());

        var result = await handler.Handle(new ConfirmImportBatchCommand(batch.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("corregir todas las filas");
        batch.Status.Should().Be(ImportStatus.Validated);
        processor.Verify(p => p.ConfirmRowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        rows.Verify(r => r.GetValidRowsPageAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Lote_de_proveedores_con_una_fila_con_error_no_se_confirma()
    {
        var batch = ImportBatch.Create(Tenant, Company, ImportType.Suppliers, User);
        batch.AttachFile("initial-load/p.xlsx", "p.xlsx", 3, User);
        batch.MarkUploaded(User);
        batch.BeginValidating(User);
        batch.CompleteValidation(totalRows: 2, validRows: 1, issueRows: 1, warningRows: 0, User);
        var batches = new Mock<IImportBatchRepository>();
        batches.Setup(b => b.GetByIdAsync(batch.Id, Tenant, Company, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);
        batches.Setup(b => b.GetByIdForUpdateAsync(batch.Id, Tenant, Company, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);
        var processor = new Mock<IImportProcessor>();
        var ctx = new Mock<IOperationalContext>();
        ctx.Setup(c => c.TenantId).Returns(Tenant);
        ctx.Setup(c => c.CompanyId).Returns(Company);
        ctx.Setup(c => c.UserId).Returns(User);
        var handler = new ConfirmImportBatchHandler(batches.Object, Mock.Of<IImportBatchRowRepository>(),
            Mock.Of<IImportBatchIssueRepository>(),
            new Dictionary<ImportType, IImportProcessor> { [ImportType.Suppliers] = processor.Object },
            ctx.Object, NullLogger<ConfirmImportBatchHandler>.Instance, Mock.Of<IUnitOfWork>());

        var result = await handler.Handle(new ConfirmImportBatchCommand(batch.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("corregir todas las filas");
        batch.Status.Should().Be(ImportStatus.Validated);
        processor.Verify(p => p.ConfirmRowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
