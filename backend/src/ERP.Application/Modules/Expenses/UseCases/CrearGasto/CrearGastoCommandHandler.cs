using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Expenses.UseCases.CrearGasto;

public sealed class CrearGastoCommandHandler
    : IRequestHandler<CrearGastoCommand, Result<ExpenseInvoiceDto>>
{
    private readonly IExpenseInvoiceRepository   _gastos;
    private readonly ISupplierRepository    _proveedorRepo;
    private readonly IXmlFacturaParser       _parser;
    private readonly IFileStorage            _storage;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;
    private readonly IUnitOfWork             _unitOfWork;
    private readonly ILogger<CrearGastoCommandHandler> _logger;

    public CrearGastoCommandHandler(
        IExpenseInvoiceRepository gastos,
        ISupplierRepository proveedorRepo,
        IXmlFacturaParser parser,
        IFileStorage storage,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user,
        IUnitOfWork unitOfWork,
        ILogger<CrearGastoCommandHandler> logger)
    {
        _gastos        = gastos;
        _proveedorRepo = proveedorRepo;
        _parser        = parser;
        _storage       = storage;
        _activity      = activity;
        _tenant        = tenant;
        _user          = user;
        _unitOfWork    = unitOfWork;
        _logger        = logger;
    }

    public Task<Result<ExpenseInvoiceDto>> Handle(CrearGastoCommand command, CancellationToken ct)
        => command.Modo == ModoCreacionGasto.Xml
            ? HandleXml(command, ct)
            : HandleManual(command, ct);

    private async Task<Result<ExpenseInvoiceDto>> HandleXml(CrearGastoCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        FacturaParseResult parsed;
        try
        {
            using var stream = new MemoryStream(command.XmlContent!);
            parsed = await _parser.ParseAsync(stream, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Fallo al parsear XML de gasto (tenant {TenantId}, usuario {UserId}).",
                tenantId, userId);
            return Result<ExpenseInvoiceDto>.Failure($"Error al leer el XML: {ex.Message}");
        }

        if (await _gastos.ExistsAccessKeyAsync(tenantId, parsed.AccessKey, ct))
            return Result<ExpenseInvoiceDto>.Failure(
                $"Ya existe un gasto registrado con la clave de acceso '{parsed.AccessKey}'.");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var Supplier = await ObtenerOCrearProveedor(
                tenantId, parsed.SupplierRuc, parsed.SupplierLegalName, userId, ct);

            var xmlPath = $"facturas/gastos/{parsed.AccessKey}.xml";
            using (var xmlStream = new MemoryStream(command.XmlContent!))
                await _storage.SaveAsync(xmlPath, xmlStream, ct);

            var concepto = parsed.Items.Count > 0
                ? parsed.Items[0].Description.Trim()
                : $"Gasto {parsed.InvoiceNumber}";

            ExpenseInvoice gasto;
            try
            {
                gasto = ExpenseInvoice.CreateFromXml(
                    tenantId,
                    Supplier.Id,
                    parsed.AccessKey,
                    parsed.InvoiceNumber,
                    parsed.IssueDate,
                    concepto,
                    command.Category!.Trim(),
                    parsed.Subtotal,
                    parsed.VatTotal,
                    parsed.Total,
                    xmlPath,
                    command.Notes,
                    userId);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync(ct);
                _logger.LogWarning(
                    ex,
                    "Fallo al construir gasto desde XML parseado (tenant {TenantId}, clave {Clave}).",
                    tenantId, parsed.AccessKey);
                return Result<ExpenseInvoiceDto>.Failure(ex.Message);
            }

            await _gastos.AddAsync(gasto, ct);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _user.Email, _user.FullName,
                module: "gastos", action: "gasto.crear.xml",
                entityType: "ExpenseInvoice", entityId: gasto.Id,
                description: $"{parsed.InvoiceNumber} — {Supplier.LegalName}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Gasto creado desde XML: id {GastoId}, tenant {TenantId}, clave {ClaveAcceso}.",
                gasto.Id, tenantId, parsed.AccessKey);

            return Result<ExpenseInvoiceDto>.Success(ToDto(gasto));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear gasto desde XML (tenant {TenantId})", tenantId);
            return Result<ExpenseInvoiceDto>.Failure($"No se pudo registrar el gasto: {ex.Message}");
        }
    }

    private async Task<Result<ExpenseInvoiceDto>> HandleManual(CrearGastoCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        if (command.Total!.Value > ExpenseInvoice.RequiresXmlThreshold)
            return Result<ExpenseInvoiceDto>.Failure(
                $"Los gastos con total estrictamente mayor a {ExpenseInvoice.RequiresXmlThreshold} deben registrarse con comprobante XML.");

        Guid? proveedorId = command.SupplierId;
        if (proveedorId.HasValue)
        {
            var p = await _proveedorRepo.GetByIdAsync(tenantId, proveedorId.Value, ct);
            if (p is null)
                return Result<ExpenseInvoiceDto>.Failure("Supplier no encontrado en el tenant.");
            if (!p.IsActive)
                return Result<ExpenseInvoiceDto>.Failure("El Supplier está deshabilitado.");
        }

        ExpenseInvoice gasto;
        try
        {
            gasto = ExpenseInvoice.CreateManual(
                tenantId,
                proveedorId,
                command.IssueDate!.Value,
                command.Concept!,
                command.Category!,
                command.Subtotal!.Value,
                command.VatTotal!.Value,
                command.Total!.Value,
                command.Notes,
                userId);
        }
        catch (Exception ex)
        {
            return Result<ExpenseInvoiceDto>.Failure(ex.Message);
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _gastos.AddAsync(gasto, ct);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _user.Email, _user.FullName,
                module: "gastos", action: "gasto.crear.manual",
                entityType: "ExpenseInvoice", entityId: gasto.Id,
                description: gasto.Concept), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Gasto creado manual: id {GastoId}, tenant {TenantId}, concepto {Concepto}.",
                gasto.Id, tenantId, gasto.Concept);

            return Result<ExpenseInvoiceDto>.Success(ToDto(gasto));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear gasto manual (tenant {TenantId})", tenantId);
            return Result<ExpenseInvoiceDto>.Failure($"No se pudo registrar el gasto: {ex.Message}");
        }
    }

    private async Task<Supplier> ObtenerOCrearProveedor(
        Guid tenantId, string ruc, string razonSocial, Guid userId, CancellationToken ct)
    {
        var existentes = await _proveedorRepo.GetAsync(
            tenantId, activeFilter: null, search: ruc, personType: null, ct);

        var existente = existentes.FirstOrDefault(p => p.Ruc == ruc);
        if (existente is not null) return existente;

        var tipo = ruc[2] - '0' is >= 0 and <= 5 ? Supplier.TypeNatural : Supplier.TypeLegal;
        var nuevo = Supplier.Create(
            tenantId, tipo, razonSocial, ruc,
            email: null, phone: null, address: null,
            paymentTerms: "Contado", userId);

        await _proveedorRepo.AddAsync(nuevo, ct);
        return nuevo;
    }

    private static ExpenseInvoiceDto ToDto(ExpenseInvoice g) => new(
        g.Id,
        g.AccessKey,
        g.IssueDate,
        g.SupplierId,
        g.InvoiceNumber,
        g.Concept,
        g.Category,
        g.Subtotal,
        g.TaxTotal,
        g.Total,
        g.Status,
        g.XmlPath,
        g.Notes,
        g.JournalEntryId,
        g.CreatedAt);
}
