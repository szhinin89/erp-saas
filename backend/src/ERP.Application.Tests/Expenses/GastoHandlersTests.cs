using FluentAssertions;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Expenses.UseCases.AprobarGasto;
using ERP.Application.Modules.Expenses.UseCases.CrearGasto;
using ERP.Application.Modules.Expenses.UseCases.ValidarGasto;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Gastos;

public sealed class GastoHandlersTests
{
    private static string ClaveAcceso49() => new string('7', ExpenseInvoice.AccessKeyLen);

    [Fact]
    public async Task Crear_manual_total_menor_umbral_queda_en_borrador()
    {
        var tenantId = Guid.NewGuid();
        var userId   = Guid.NewGuid();

        ExpenseInvoice? guardado = null;
        var gastos = new Mock<IExpenseInvoiceRepository>();
        gastos.Setup(x => x.AddAsync(It.IsAny<ExpenseInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<ExpenseInvoice, CancellationToken>((g, _) => guardado = g)
            .Returns(Task.CompletedTask);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        uow.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var prov = new Mock<ISupplierRepository>();
        var parser = new Mock<IXmlFacturaParser>();
        var storage = new Mock<IFileStorage>();
        var activity = new Mock<IUserActivityRepository>();
        activity.Setup(x => x.AddAsync(It.IsAny<ERP.Domain.Audit.Entities.UserActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(x => x.TenantId).Returns(tenantId);
        var user = new Mock<ICurrentUser>();
        user.SetupGet(x => x.UserId).Returns(userId);

        var handler = new CrearGastoCommandHandler(
            gastos.Object, prov.Object, parser.Object, storage.Object, activity.Object, tenant.Object, user.Object,
            uow.Object,
            NullLogger<CrearGastoCommandHandler>.Instance);

        var cmd = new CrearGastoCommand(
            ModoCreacionGasto.Manual,
            XmlContent: null,
            XmlNombreArchivo: null,
            SupplierId: null,
            IssueDate: DateTime.UtcNow.Date,
            Concept: "Papelería",
            Category: "Administrativo",
            Subtotal: 80m,
            VatTotal: 12m,
            Total: 92m,
            Notes: null);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        guardado.Should().NotBeNull();
        guardado!.Status.Should().Be(ExpenseStatus.Draft);
        guardado.Total.Should().Be(92m);
    }

    [Fact]
    public async Task Crear_manual_total_mayor_umbral_sin_xml_falla()
    {
        var tenantId = Guid.NewGuid();
        var userId   = Guid.NewGuid();

        var gastos = new Mock<IExpenseInvoiceRepository>();
        var prov = new Mock<ISupplierRepository>();
        var parser = new Mock<IXmlFacturaParser>();
        var storage = new Mock<IFileStorage>();
        var activity = new Mock<IUserActivityRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(x => x.TenantId).Returns(tenantId);
        var user = new Mock<ICurrentUser>();
        user.SetupGet(x => x.UserId).Returns(userId);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        uow.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new CrearGastoCommandHandler(
            gastos.Object, prov.Object, parser.Object, storage.Object, activity.Object, tenant.Object, user.Object,
            uow.Object,
            NullLogger<CrearGastoCommandHandler>.Instance);

        var cmd = new CrearGastoCommand(
            ModoCreacionGasto.Manual,
            null, null, null,
            DateTime.UtcNow.Date,
            "Equipo",
            "CAPEX",
            Subtotal: 200m,
            VatTotal: 24m,
            Total: 224m,
            null);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("100");
        gastos.Verify(x => x.AddAsync(It.IsAny<ExpenseInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Crear_xml_parsea_y_guarda_borrador_con_total_sobre_umbral()
    {
        var tenantId = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var clave    = ClaveAcceso49();
        var ruc      = "1790016918001";

        var parsed = new FacturaParseResult(
            clave,
            "001-001-000099",
            DateTime.UtcNow.Date,
            ruc,
            "Proveedor XML SA",
            Subtotal: 400m,
            VatTotal: 48m,
            Total: 448m,
            Items: new[] { new ItemFactura("X", "Servicio cloud", 1m, 400m, 0m, 400m) });

        var gastos = new Mock<IExpenseInvoiceRepository>();
        gastos.Setup(x => x.ExistsAccessKeyAsync(tenantId, clave, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        ExpenseInvoice? guardado = null;
        gastos.Setup(x => x.AddAsync(It.IsAny<ExpenseInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<ExpenseInvoice, CancellationToken>((g, _) => guardado = g)
            .Returns(Task.CompletedTask);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        uow.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var prov = new Mock<ISupplierRepository>();
        prov.Setup(x => x.GetAsync(tenantId, null, ruc, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Supplier>());
        Supplier? nuevoProv = null;
        prov.Setup(x => x.AddAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Callback<Supplier, CancellationToken>((p, _) => nuevoProv = p)
            .Returns(Task.CompletedTask);

        var parser = new Mock<IXmlFacturaParser>();
        parser.Setup(x => x.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parsed);

        var storage = new Mock<IFileStorage>();
        storage.Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, Stream _, CancellationToken _) => path);

        var activity = new Mock<IUserActivityRepository>();
        activity.Setup(x => x.AddAsync(It.IsAny<ERP.Domain.Audit.Entities.UserActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(x => x.TenantId).Returns(tenantId);
        var user = new Mock<ICurrentUser>();
        user.SetupGet(x => x.UserId).Returns(userId);

        var handler = new CrearGastoCommandHandler(
            gastos.Object, prov.Object, parser.Object, storage.Object, activity.Object, tenant.Object, user.Object,
            uow.Object,
            NullLogger<CrearGastoCommandHandler>.Instance);

        var xmlBytes = new byte[] { 60, 63, 120, 109, 108 }; // minimal bytes; parser is mocked
        var cmd = new CrearGastoCommand(
            ModoCreacionGasto.Xml,
            xmlBytes,
            "factura.xml",
            null, null, null,
            Category: "Servicios TI",
            null, null, null,
            null);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        guardado.Should().NotBeNull();
        guardado!.Status.Should().Be(ExpenseStatus.Draft);
        guardado.Total.Should().Be(448m);
        guardado.AccessKey.Should().Be(clave);
        nuevoProv.Should().NotBeNull();
        nuevoProv!.Ruc.Should().Be(ruc);
    }

    [Fact]
    public async Task Validar_y_aprobar_gasto_llama_contabilidad_y_marca_aprobado()
    {
        var tenantId = Guid.NewGuid();
        var userId   = Guid.NewGuid();

        var gasto = ExpenseInvoice.CreateManual(
            tenantId, supplierId: null, DateTime.UtcNow.Date, "Taxi", "Viajes",
            10m, 1.2m, 11.2m, null, userId);

        var gastos = new Mock<IExpenseInvoiceRepository>();
        gastos.Setup(x => x.GetByIdAsync(tenantId, gasto.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gasto);

        var uowValidar = new Mock<IUnitOfWork>();
        uowValidar.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uowValidar.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        uowValidar.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uowValidar.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var provRepo = new Mock<ISupplierRepository>();

        var activity = new Mock<IUserActivityRepository>();
        activity.Setup(x => x.AddAsync(It.IsAny<ERP.Domain.Audit.Entities.UserActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(x => x.TenantId).Returns(tenantId);
        var user = new Mock<ICurrentUser>();
        user.SetupGet(x => x.UserId).Returns(userId);
        user.SetupGet(x => x.Email).Returns("u@test");
        user.SetupGet(x => x.FullName).Returns("User");

        var validar = new ValidarGastoCommandHandler(
            gastos.Object, provRepo.Object, activity.Object, tenant.Object, user.Object, uowValidar.Object);
        var valRes = await validar.Handle(new ValidarGastoCommand(gasto.Id), CancellationToken.None);
        valRes.IsSuccess.Should().BeTrue();
        gasto.Status.Should().Be(ExpenseStatus.Validated);

        var asientoId = Guid.NewGuid();
        var accounting = new Mock<IAccountingService>();
        accounting.Setup(x => x.CrearAsientoGastoAsync(
                gasto.Id,
                gasto.Category,
                It.IsAny<string>(),
                gasto.IssueDate,
                gasto.Subtotal,
                gasto.TaxTotal,
                gasto.Total,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(asientoId));

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        uow.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var aprobar = new AprobarGastoCommandHandler(
            gastos.Object,
            accounting.Object,
            activity.Object,
            tenant.Object,
            user.Object,
            uow.Object,
            NullLogger<AprobarGastoCommandHandler>.Instance);

        var aprRes = await aprobar.Handle(new AprobarGastoCommand(gasto.Id), CancellationToken.None);

        aprRes.IsSuccess.Should().BeTrue();
        gasto.Status.Should().Be(ExpenseStatus.Approved);
        gasto.JournalEntryId.Should().Be(asientoId);
        accounting.Verify(x => x.CrearAsientoGastoAsync(
            gasto.Id,
            gasto.Category,
            It.IsAny<string>(),
            gasto.IssueDate,
            gasto.Subtotal,
            gasto.TaxTotal,
            gasto.Total,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

