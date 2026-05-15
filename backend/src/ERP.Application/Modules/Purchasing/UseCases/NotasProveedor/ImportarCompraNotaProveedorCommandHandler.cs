using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.NotasProveedor;

public sealed class ImportarCompraNotaProveedorCommandHandler
    : IRequestHandler<ImportarCompraNotaProveedorCommand, Result<CompraNotaProveedorDto>>
{
    private readonly IPurchBillRepository        _compraRepo;
    private readonly ISupplierRepository     _proveedorRepo;
    private readonly IExpenseInvoiceRepository  _gastoRepo;
    private readonly IXmlFacturaParser       _parser;
    private readonly IFileStorage             _storage;
    private readonly IUserActivityRepository  _activity;
    private readonly ICurrentTenant             _tenant;
    private readonly ICurrentUser             _user;
    private readonly IUnitOfWork              _unitOfWork;
    private readonly ILogger<ImportarCompraNotaProveedorCommandHandler> _logger;

    public ImportarCompraNotaProveedorCommandHandler(
        IPurchBillRepository compraRepo,
        ISupplierRepository proveedorRepo,
        IExpenseInvoiceRepository gastoRepo,
        IXmlFacturaParser parser,
        IFileStorage storage,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user,
        IUnitOfWork unitOfWork,
        ILogger<ImportarCompraNotaProveedorCommandHandler> logger)
    {
        _compraRepo    = compraRepo;
        _proveedorRepo = proveedorRepo;
        _gastoRepo     = gastoRepo;
        _parser        = parser;
        _storage       = storage;
        _activity      = activity;
        _tenant        = tenant;
        _user          = user;
        _unitOfWork    = unitOfWork;
        _logger        = logger;
    }

    public async Task<Result<CompraNotaProveedorDto>> Handle(
        ImportarCompraNotaProveedorCommand command,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        if (command.PurchBillId.HasValue && command.ExpenseInvoiceId.HasValue)
            return Result<CompraNotaProveedorDto>.Failure(
                "Indique solo una factura destino: compra o gasto, no ambas.");

        NotaProveedorParseResult parsed;
        try
        {
            using var ms = new MemoryStream(command.XmlContent);
            parsed = await _parser.ParseNotaProveedorAsync(ms, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo al parsear XML de nota Supplier (tenant {TenantId}).", tenantId);
            return Result<CompraNotaProveedorDto>.Failure($"Error al leer el XML: {ex.Message}");
        }

        if (await _compraRepo.ExistsPurchNoteAccessKeyAsync(tenantId, parsed.AccessKey, ct))
            return Result<CompraNotaProveedorDto>.Failure(
                $"Ya existe una nota de Supplier con la clave de acceso '{parsed.AccessKey}'.");

        var Supplier = await _proveedorRepo.GetByRucAsync(tenantId, parsed.SupplierRuc, ct);
        if (Supplier is null)
            return Result<CompraNotaProveedorDto>.Failure(
                $"No hay Supplier registrado con RUC '{parsed.SupplierRuc}'. Cree el Supplier antes de importar la nota.");

        PurchBill? compra = null;
        IReadOnlyList<PurchWarehouseAlloc> compraAsignaciones = Array.Empty<PurchWarehouseAlloc>();
        if (command.PurchBillId.HasValue)
        {
            compra = await _compraRepo.GetByIdAsync(tenantId, command.PurchBillId.Value, ct);
            if (compra is null)
                return Result<CompraNotaProveedorDto>.Failure("Factura de compra no encontrada.");
            if (compra.SupplierId != Supplier.Id)
                return Result<CompraNotaProveedorDto>.Failure(
                    "El Supplier de la nota no coincide con el de la factura de compra.");
            if (compra.Status != PurchaseStatus.Approved)
                return Result<CompraNotaProveedorDto>.Failure(
                    "Solo se pueden vincular notas a una compra en estado Aprobado.");

            compraAsignaciones = await _compraRepo.GetWarehouseAllocsByBillIdAsync(
                tenantId, compra.Id, ct);
        }

        if (command.ExpenseInvoiceId.HasValue)
        {
            var gasto = await _gastoRepo.GetByIdAsync(tenantId, command.ExpenseInvoiceId.Value, ct);
            if (gasto is null)
                return Result<CompraNotaProveedorDto>.Failure("Factura de gasto no encontrada.");
            if (gasto.SupplierId != Supplier.Id)
                return Result<CompraNotaProveedorDto>.Failure(
                    "El Supplier de la nota no coincide con el del gasto.");
            if (gasto.Status != ExpenseStatus.Approved)
                return Result<CompraNotaProveedorDto>.Failure(
                    "Solo se pueden vincular notas a un gasto en estado Aprobado.");
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var fileName = string.IsNullOrWhiteSpace(command.XmlNombreArchivo)
                ? $"{parsed.AccessKey}.xml"
                : command.XmlNombreArchivo.Trim();
            var xmlPath = $"compras/notas-Supplier/{parsed.AccessKey}/{fileName}";
            using (var xmlStream = new MemoryStream(command.XmlContent))
                await _storage.SaveAsync(xmlPath, xmlStream, ct);

            var nota = PurchNote.Create(
                tenantId,
                Supplier.Id,
                command.PurchBillId,
                command.ExpenseInvoiceId,
                parsed.NoteType,
                parsed.Reason,
                parsed.AccessKey,
                parsed.IssueDate,
                parsed.EstabCode,
                parsed.EmPointCode,
                parsed.Sequential,
                userId);
            nota.SetXmlPath(xmlPath, userId);

            foreach (var item in parsed.Items)
            {
                Guid? productoId = null;
                if (compra is not null && !string.IsNullOrEmpty(item.ProductCode))
                {
                    var det = compra.Lines.FirstOrDefault(d =>
                        string.Equals(d.SupplierProductCode, item.ProductCode, StringComparison.OrdinalIgnoreCase));
                    productoId = det?.ProductId;
                    if (!productoId.HasValue && det is not null)
                    {
                        productoId = compraAsignaciones
                            .FirstOrDefault(a => a.PurchBillLineId == det.Id && a.ProductId.HasValue)
                            ?.ProductId;
                    }
                }

                var linea = PurchNoteLine.Create(
                    tenantId,
                    productoId,
                    string.IsNullOrEmpty(item.ProductCode) ? null : item.ProductCode,
                    item.Description,
                    item.Quantity,
                    item.UnitPrice,
                    item.Subtotal,
                    item.VatAmount,
                    item.Total,
                    userId);
                nota.AddLine(linea);
            }

            await _compraRepo.AddPurchNoteAsync(nota, ct);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _user.Email, _user.FullName,
                module: "compras", action: "notas-Supplier.importar",
                entityType: "PurchNote", entityId: nota.Id,
                description: $"{parsed.NoteType} {parsed.Sequential} — clave {parsed.AccessKey}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Nota Supplier importada: id {NotaId}, tenant {TenantId}, tipo {Tipo}.",
                nota.Id, tenantId, parsed.NoteType);

            return Result<CompraNotaProveedorDto>.Success(ToDto(nota));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al importar nota Supplier");
            return Result<CompraNotaProveedorDto>.Failure($"No se pudo importar la nota: {ex.Message}");
        }
    }

    private static CompraNotaProveedorDto ToDto(PurchNote n) => new(
        n.Id,
        n.SupplierId,
        n.PurchBillId,
        n.ExpenseInvoiceId,
        n.NoteType,
        n.Reason,
        n.AccessKey,
        n.IssueDate,
        n.EstabCode,
        n.EmPointCode,
        n.Sequential,
        n.Subtotal,
        n.VatTotal,
        n.Total,
        n.Status,
        n.XmlPath,
        n.JournalEntryId,
        n.CreatedAt);
}
