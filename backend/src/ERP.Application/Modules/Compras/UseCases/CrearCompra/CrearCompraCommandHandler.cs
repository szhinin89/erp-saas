using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Compras;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventario.Interfaces;
using ERP.Domain.Modules.Compras.Entities;
using ERP.Domain.Modules.Compras.Interfaces;
using ERP.Domain.Modules.Compras.Entities;
using ERP.Domain.Modules.Compras.Interfaces;

namespace ERP.Application.Modules.Compras.UseCases.CrearCompra;

public sealed class CrearCompraCommandHandler
    : IRequestHandler<CrearCompraCommand, Result<CompraFacturaDto>>
{
    private readonly ICompraRepository       _compraRepo;
    private readonly IProveedorRepository    _proveedorRepo;
    private readonly IBodegaRepository        _bodegaRepo;
    private readonly IXmlFacturaParser       _parser;
    private readonly IFileStorage            _storage;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;
    private readonly IUnitOfWork             _unitOfWork;
    private readonly ILogger<CrearCompraCommandHandler> _logger;

    public CrearCompraCommandHandler(
        ICompraRepository repo,
        IProveedorRepository proveedorRepo,
        IBodegaRepository bodegaRepo,
        IXmlFacturaParser parser,
        IFileStorage storage,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user,
        IUnitOfWork unitOfWork,
        ILogger<CrearCompraCommandHandler> logger)
    {
        _compraRepo    = repo;
        _proveedorRepo = proveedorRepo;
        _bodegaRepo    = bodegaRepo;
        _parser        = parser;
        _storage       = storage;
        _activity      = activity;
        _tenant        = tenant;
        _user          = user;
        _unitOfWork    = unitOfWork;
        _logger        = logger;
    }

    public async Task<Result<CompraFacturaDto>> Handle(
        CrearCompraCommand command, CancellationToken ct)
    {
        return command.Modo == ModoCreacionCompra.Xml
            ? await HandleXml(command, ct)
            : await HandleManual(command, ct);
    }

    // ── Modo XML ─────────────────────────────────────────────────────────

    private async Task<Result<CompraFacturaDto>> HandleXml(
        CrearCompraCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
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
                "Fallo al parsear XML de compra (tenant {TenantId}, usuario {UserId}).",
                tenantId, userId);
            return Result<CompraFacturaDto>.Failure($"Error al leer el XML: {ex.Message}");
        }

        // 2. Verificar clave acceso única en el tenant
        if (await _compraRepo.ExistsClaveAccesoAsync(tenantId, parsed.ClaveAcceso, ct))
            return Result<CompraFacturaDto>.Failure(
                $"Ya existe una compra registrada con la clave de acceso '{parsed.ClaveAcceso}'.");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // 3. Buscar o crear proveedor por RUC
            var proveedor = await ObtenerOCrearProveedor(
                tenantId, parsed.RucProveedor, parsed.RazonSocialProveedor, userId, ct);

            // 4. Guardar archivo XML (fuera de SQL; si falla el commit, puede quedar huérfano en almacén)
            var xmlPath = $"facturas/compras/{parsed.ClaveAcceso}.xml";
            using (var xmlStream = new MemoryStream(command.XmlContent!))
                await _storage.SaveAsync(xmlPath, xmlStream, ct);

            // 5. Crear CompraFactura
            var compra = CompraFactura.Create(
                tenantId, proveedor.Id,
                parsed.NumeroFactura, parsed.ClaveAcceso, xmlPath,
                parsed.FechaEmision, null,
                "Contado", null, userId);

            foreach (var item in parsed.Items)
            {
                compra.AgregarDetalle(
                    item.Descripcion, item.CodigoPrincipal,
                    productoId: null,
                    item.Cantidad, item.PrecioUnitario,
                    item.Descuento > 0
                        ? Math.Round(item.Descuento / (item.Cantidad * item.PrecioUnitario) * 100, 4)
                        : 0m,
                    ivaPorcentaje: 0m,   // se recalcula en validación desde XML totales
                    userId);
            }

            if (command.AsignacionesBodega is { Count: > 0 })
            {
                var detalleList = compra.Detalles.ToList();
                var errXml = await CompraAsignacionBodegasRules.ValidateAgainstDetallesAsync(
                    detalleList, command.AsignacionesBodega, tenantId, _bodegaRepo, ct);
                if (errXml is not null)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    return Result<CompraFacturaDto>.Failure(errXml);
                }
            }

            await _compraRepo.AddAsync(compra, ct);

            if (command.AsignacionesBodega is { Count: > 0 })
            {
                var detalleList = compra.Detalles.ToList();
                foreach (var a in command.AsignacionesBodega)
                {
                    var det = detalleList[a.ItemIndex];
                    var productoAsignado = a.ProductoId ?? det.ProductoId;
                    var row = CompraBodegaAsignacion.Create(
                        tenantId, compra.Id, det.Id, a.BodegaId, productoAsignado, a.Cantidad, userId);
                    await _compraRepo.AddBodegaAsignacionAsync(row, ct);
                }
            }

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _user.Email, _user.FullName,
                module: "compras", action: "compra.crear.xml",
                entityType: "CompraFactura", entityId: compra.Id,
                description: $"{parsed.NumeroFactura} — {proveedor.RazonSocial}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Compra creada desde XML: id {CompraId}, tenant {TenantId}, clave {ClaveAcceso}, número {Numero}.",
                compra.Id, tenantId, parsed.ClaveAcceso, parsed.NumeroFactura);

            return Result<CompraFacturaDto>.Success(ToDto(compra));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear compra desde XML (tenant {TenantId})", tenantId);
            return Result<CompraFacturaDto>.Failure($"No se pudo registrar la compra: {ex.Message}");
        }
    }

    // ── Modo Manual ───────────────────────────────────────────────────────

    private async Task<Result<CompraFacturaDto>> HandleManual(
        CrearCompraCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var proveedor = await _proveedorRepo.GetByIdAsync(tenantId, command.ProveedorId!.Value, ct);
        if (proveedor is null)
            return Result<CompraFacturaDto>.Failure("Proveedor no encontrado en el tenant.");
        if (!proveedor.IsActive)
            return Result<CompraFacturaDto>.Failure("El proveedor está deshabilitado.");

        var compra = CompraFactura.Create(
            tenantId, proveedor.Id,
            command.NumeroFactura!, null, null,
            command.FechaFactura!.Value, command.FechaVencimiento,
            command.CondicionPago!, command.Observaciones, userId);

        foreach (var d in command.Detalles!)
        {
            compra.AgregarDetalle(
                d.Descripcion, d.CodigoPrincipal, d.ProductoId,
                d.Cantidad, d.PrecioUnitario,
                d.DescuentoPorcentaje, d.IvaPorcentaje, userId);
        }

        if (command.AsignacionesBodega is { Count: > 0 })
        {
            var err = await CompraAsignacionBodegasRules.ValidateAsync(
                command.Detalles!, command.AsignacionesBodega, tenantId, _bodegaRepo, ct);
            if (err is not null)
                return Result<CompraFacturaDto>.Failure(err);
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _compraRepo.AddAsync(compra, ct);

            if (command.AsignacionesBodega is { Count: > 0 })
            {
                var detalleList = compra.Detalles.ToList();
                foreach (var a in command.AsignacionesBodega)
                {
                    var det = detalleList[a.ItemIndex];
                    var productoAsignado = a.ProductoId ?? det.ProductoId;
                    var row = CompraBodegaAsignacion.Create(
                        tenantId, compra.Id, det.Id, a.BodegaId, productoAsignado, a.Cantidad, userId);
                    await _compraRepo.AddBodegaAsignacionAsync(row, ct);
                }
            }

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _user.Email, _user.FullName,
                module: "compras", action: "compra.crear.manual",
                entityType: "CompraFactura", entityId: compra.Id,
                description: $"{compra.NumeroFactura} — {proveedor.RazonSocial}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Compra creada manual: id {CompraId}, tenant {TenantId}, número {Numero}.",
                compra.Id, tenantId, compra.NumeroFactura);

            return Result<CompraFacturaDto>.Success(ToDto(compra));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear compra manual (tenant {TenantId})", tenantId);
            return Result<CompraFacturaDto>.Failure($"No se pudo registrar la compra: {ex.Message}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<Proveedor> ObtenerOCrearProveedor(
        Guid tenantId, string ruc, string razonSocial, Guid userId, CancellationToken ct)
    {
        var existentes = await _proveedorRepo.GetAsync(
            tenantId, activeFilter: null, search: ruc, tipoPersona: null, ct);

        var existente = existentes.FirstOrDefault(p => p.Ruc == ruc);
        if (existente is not null) return existente;

        // Crear proveedor básico (el usuario podrá completar los datos después)
        var tipo = ruc[2] - '0' is >= 0 and <= 5 ? Proveedor.TipoNatural : Proveedor.TipoJuridica;
        var nuevo = Proveedor.Create(
            tenantId, tipo, razonSocial, ruc,
            correo: null, telefono: null, direccion: null,
            condicionPago: "Contado", userId);

        await _proveedorRepo.AddAsync(nuevo, ct);
        return nuevo;
    }

    private static CompraFacturaDto ToDto(CompraFactura c) => new(
        c.Id, c.ProveedorId, c.NumeroFactura, c.ClaveAcceso, c.XmlPath,
        c.FechaFactura, c.FechaVencimiento, c.Estado, c.CondicionPago,
        c.Subtotal, c.IvaTotal, c.Total, c.Observaciones, c.AsientoContableId, c.CreatedAt);
}
