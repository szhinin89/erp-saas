using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Purchasing;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.CrearCompra;

public sealed class CreatePurchaseCommandHandler
    : IRequestHandler<CreatePurchaseCommand, Result<PurchBillDto>>
{
    private readonly IPurchBillRepository       _compraRepo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IWarehouseRepository        _bodegaRepo;
    private readonly IXmlFacturaParser       _parser;
    private readonly IFileStorage            _storage;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _subscriber;
    private readonly ICurrentUser            _user;
    private readonly IUnitOfWork             _unitOfWork;
    private readonly ILogger<CreatePurchaseCommandHandler> _logger;

    public CreatePurchaseCommandHandler(
        IPurchBillRepository repo,
        IBusinessPartnerRepository bpRepo,
        IWarehouseRepository bodegaRepo,
        IXmlFacturaParser parser,
        IFileStorage storage,
        IUserActivityRepository activity,
        ICurrentSubscriber subscriber,
        ICurrentUser user,
        IUnitOfWork unitOfWork,
        ILogger<CreatePurchaseCommandHandler> logger)
    {
        _compraRepo = repo;
        _bpRepo     = bpRepo;
        _bodegaRepo    = bodegaRepo;
        _parser        = parser;
        _storage       = storage;
        _activity      = activity;
        _subscriber = subscriber;
        _user          = user;
        _unitOfWork    = unitOfWork;
        _logger        = logger;
    }

    public async Task<Result<PurchBillDto>> Handle(
        CreatePurchaseCommand command, CancellationToken ct)
    {
        return command.Modo == PurchaseCreationMode.Xml
            ? await HandleXml(command, ct)
            : await HandleManual(command, ct);
    }

    // ── Modo XML ─────────────────────────────────────────────────────────

    private async Task<Result<PurchBillDto>> HandleXml(
        CreatePurchaseCommand command, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var userId   = _user.UserId;

        // 1. Parsear XML
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
                "Fallo al parsear XML de compra (tenant {SubscriberId}, usuario {UserId}).",
                subscriberId, userId);
            return Result<PurchBillDto>.Failure($"Error al leer el XML: {ex.Message}");
        }

        // 2. Verificar clave acceso única en el tenant
        if (await _compraRepo.ExistsAccessKeyAsync(subscriberId, parsed.AccessKey, ct))
            return Result<PurchBillDto>.Failure(
                $"Ya existe una compra registrada con la clave de acceso '{parsed.AccessKey}'.");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // 3. Buscar o crear BusinessPartner por RUC
            var bp = await ObtenerOCrearBusinessPartner(
                subscriberId, parsed.SupplierRuc, parsed.SupplierLegalName, userId, ct);

            // 4. Guardar archivo XML
            var xmlPath = $"facturas/compras/{parsed.AccessKey}.xml";
            using (var xmlStream = new MemoryStream(command.XmlContent!))
                await _storage.SaveAsync(xmlPath, xmlStream, ct);

            // 5. Crear PurchBill
            var compra = PurchBill.Create(
                subscriberId, bp.Id,
                parsed.InvoiceNumber, parsed.AccessKey, xmlPath,
                parsed.IssueDate, null,
                "Contado", null, userId);

            foreach (var item in parsed.Items)
            {
                compra.AddLine(
                    item.Description, item.ProductCode,
                    productId: null,
                    item.Quantity, item.UnitPrice,
                    item.Discount > 0
                        ? Math.Round(item.Discount / (item.Quantity * item.UnitPrice) * 100, 4)
                        : 0m,
                    vatPct: 0m,   // se recalcula en validación desde XML totales
                    userId);
            }

            if (command.WarehouseAllocations is { Count: > 0 })
            {
                var detalleList = compra.Lines.ToList();
                var errXml = await PurchaseAsignacionWarehousesRules.ValidateAgainstDetallesAsync(
                    detalleList, command.WarehouseAllocations, subscriberId, _bodegaRepo, ct);
                if (errXml is not null)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    return Result<PurchBillDto>.Failure(errXml);
                }
            }

            await _compraRepo.AddAsync(compra, ct);

            if (command.WarehouseAllocations is { Count: > 0 })
            {
                var detalleList = compra.Lines.ToList();
                foreach (var a in command.WarehouseAllocations)
                {
                    var det = detalleList[a.ItemIndex];
                    var productoAsignado = a.ProductId ?? det.ProductId;
                    var row = PurchWarehouseAlloc.Create(
                        subscriberId, compra.Id, det.Id, a.WarehouseId, productoAsignado, a.Quantity, userId);
                    await _compraRepo.AddWarehouseAllocAsync(row, ct);
                }
            }

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _user.Email, _user.FullName,
                module: "compras", action: "compra.crear.xml",
                entityType: "PurchBill", entityId: compra.Id,
                description: $"{parsed.InvoiceNumber} — {bp.LegalName}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Compra creada desde XML: id {CompraId}, tenant {SubscriberId}, clave {AccessKey}, número {Numero}.",
                compra.Id, subscriberId, parsed.AccessKey, parsed.InvoiceNumber);

            return Result<PurchBillDto>.Success(ToDto(compra));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear compra desde XML (tenant {SubscriberId})", subscriberId);
            return Result<PurchBillDto>.Failure($"No se pudo registrar la compra: {ex.Message}");
        }
    }

    // ── Modo Manual ───────────────────────────────────────────────────────

    private async Task<Result<PurchBillDto>> HandleManual(
        CreatePurchaseCommand command, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var userId   = _user.UserId;

        var bp = await _bpRepo.GetByIdAsync(command.BusinessPartnerId!.Value, ct);
        if (bp is null)
            return Result<PurchBillDto>.Failure("Proveedor no encontrado.");
        if (!bp.IsActive)
            return Result<PurchBillDto>.Failure("El proveedor está deshabilitado.");

        var compra = PurchBill.Create(
            subscriberId, bp.Id,
            command.InvoiceNumber!, null, null,
            command.InvoiceDate!.Value, command.DueDate,
            command.PaymentTerms!, command.Notes, userId);

        foreach (var d in command.Lines!)
        {
            compra.AddLine(
                d.Description, d.ProductCode, d.ProductId,
                d.Quantity, d.UnitPrice,
                d.DiscountPct, d.VatPct, userId);
        }

        if (command.WarehouseAllocations is { Count: > 0 })
        {
            var err = await PurchaseAsignacionWarehousesRules.ValidateAsync(
                command.Lines!, command.WarehouseAllocations, subscriberId, _bodegaRepo, ct);
            if (err is not null)
                return Result<PurchBillDto>.Failure(err);
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _compraRepo.AddAsync(compra, ct);

            if (command.WarehouseAllocations is { Count: > 0 })
            {
                var detalleList = compra.Lines.ToList();
                foreach (var a in command.WarehouseAllocations)
                {
                    var det = detalleList[a.ItemIndex];
                    var productoAsignado = a.ProductId ?? det.ProductId;
                    var row = PurchWarehouseAlloc.Create(
                        subscriberId, compra.Id, det.Id, a.WarehouseId, productoAsignado, a.Quantity, userId);
                    await _compraRepo.AddWarehouseAllocAsync(row, ct);
                }
            }

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _user.Email, _user.FullName,
                module: "compras", action: "compra.crear.manual",
                entityType: "PurchBill", entityId: compra.Id,
                description: $"{compra.InvoiceNumber} — {bp.LegalName}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Compra creada manual: id {CompraId}, tenant {SubscriberId}, número {Numero}.",
                compra.Id, subscriberId, compra.InvoiceNumber);

            return Result<PurchBillDto>.Success(ToDto(compra));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear compra manual (tenant {SubscriberId})", subscriberId);
            return Result<PurchBillDto>.Failure($"No se pudo registrar la compra: {ex.Message}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<BusinessPartner> ObtenerOCrearBusinessPartner(
        Guid subscriberId, string ruc, string razonSocial, Guid userId, CancellationToken ct)
    {
        var existente = await _bpRepo.GetByIdentificationAsync("RUC", ruc, ct);
        if (existente is not null) return existente;

        var nuevo = BusinessPartner.Create(
            subscriberId, "RUC", ruc, razonSocial, userId);
        await _bpRepo.AddAsync(nuevo, ct);
        return nuevo;
    }

    private static PurchBillDto ToDto(PurchBill c) => new(
        c.Id, c.BusinessPartnerId, c.InvoiceNumber, c.AccessKey, c.XmlPath,
        c.InvoiceDate, c.DueDate, c.Status, c.PaymentTerms,
        c.Subtotal, c.VatTotal, c.Total, c.Notes, c.JournalEntryId, c.CreatedAt);
}
