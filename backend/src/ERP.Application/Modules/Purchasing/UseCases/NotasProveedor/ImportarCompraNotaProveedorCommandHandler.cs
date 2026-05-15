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
    private readonly ICompraRepository        _compraRepo;
    private readonly IProveedorRepository     _proveedorRepo;
    private readonly IGastoFacturaRepository  _gastoRepo;
    private readonly IXmlFacturaParser       _parser;
    private readonly IFileStorage             _storage;
    private readonly IUserActivityRepository  _activity;
    private readonly ICurrentTenant             _tenant;
    private readonly ICurrentUser             _user;
    private readonly IUnitOfWork              _unitOfWork;
    private readonly ILogger<ImportarCompraNotaProveedorCommandHandler> _logger;

    public ImportarCompraNotaProveedorCommandHandler(
        ICompraRepository compraRepo,
        IProveedorRepository proveedorRepo,
        IGastoFacturaRepository gastoRepo,
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

        if (command.CompraFacturaId.HasValue && command.GastoFacturaId.HasValue)
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
            _logger.LogWarning(ex, "Fallo al parsear XML de nota proveedor (tenant {TenantId}).", tenantId);
            return Result<CompraNotaProveedorDto>.Failure($"Error al leer el XML: {ex.Message}");
        }

        if (await _compraRepo.ExistsNotaProveedorClaveAccesoAsync(tenantId, parsed.ClaveAcceso, ct))
            return Result<CompraNotaProveedorDto>.Failure(
                $"Ya existe una nota de proveedor con la clave de acceso '{parsed.ClaveAcceso}'.");

        var proveedor = await _proveedorRepo.GetByRucAsync(tenantId, parsed.RucProveedor, ct);
        if (proveedor is null)
            return Result<CompraNotaProveedorDto>.Failure(
                $"No hay proveedor registrado con RUC '{parsed.RucProveedor}'. Cree el proveedor antes de importar la nota.");

        CompraFactura? compra = null;
        IReadOnlyList<CompraBodegaAsignacion> compraAsignaciones = Array.Empty<CompraBodegaAsignacion>();
        if (command.CompraFacturaId.HasValue)
        {
            compra = await _compraRepo.GetByIdWithDetailsAsync(tenantId, command.CompraFacturaId.Value, ct);
            if (compra is null)
                return Result<CompraNotaProveedorDto>.Failure("Factura de compra no encontrada.");
            if (compra.ProveedorId != proveedor.Id)
                return Result<CompraNotaProveedorDto>.Failure(
                    "El proveedor de la nota no coincide con el de la factura de compra.");
            if (compra.Estado != EstadoCompra.Aprobado)
                return Result<CompraNotaProveedorDto>.Failure(
                    "Solo se pueden vincular notas a una compra en estado Aprobado.");

            compraAsignaciones = await _compraRepo.GetBodegaAsignacionesByCompraFacturaIdAsync(
                tenantId, compra.Id, ct);
        }

        if (command.GastoFacturaId.HasValue)
        {
            var gasto = await _gastoRepo.GetByIdAsync(tenantId, command.GastoFacturaId.Value, ct);
            if (gasto is null)
                return Result<CompraNotaProveedorDto>.Failure("Factura de gasto no encontrada.");
            if (gasto.ProveedorId != proveedor.Id)
                return Result<CompraNotaProveedorDto>.Failure(
                    "El proveedor de la nota no coincide con el del gasto.");
            if (gasto.Estado != EstadoGasto.Aprobado)
                return Result<CompraNotaProveedorDto>.Failure(
                    "Solo se pueden vincular notas a un gasto en estado Aprobado.");
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var fileName = string.IsNullOrWhiteSpace(command.XmlNombreArchivo)
                ? $"{parsed.ClaveAcceso}.xml"
                : command.XmlNombreArchivo.Trim();
            var xmlPath = $"compras/notas-proveedor/{parsed.ClaveAcceso}/{fileName}";
            using (var xmlStream = new MemoryStream(command.XmlContent))
                await _storage.SaveAsync(xmlPath, xmlStream, ct);

            var nota = CompraNotaProveedor.Create(
                tenantId,
                proveedor.Id,
                command.CompraFacturaId,
                command.GastoFacturaId,
                parsed.TipoNota,
                parsed.Motivo,
                parsed.ClaveAcceso,
                parsed.FechaEmision,
                parsed.Establecimiento,
                parsed.PuntoEmision,
                parsed.Secuencial,
                userId);
            nota.SetXmlPath(xmlPath, userId);

            foreach (var item in parsed.Items)
            {
                Guid? productoId = null;
                if (compra is not null && !string.IsNullOrEmpty(item.CodigoPrincipal))
                {
                    var det = compra.Detalles.FirstOrDefault(d =>
                        string.Equals(d.CodigoPrincipalProveedor, item.CodigoPrincipal, StringComparison.OrdinalIgnoreCase));
                    productoId = det?.ProductoId;
                    if (!productoId.HasValue && det is not null)
                    {
                        productoId = compraAsignaciones
                            .FirstOrDefault(a => a.CompraDetalleId == det.Id && a.ProductoId.HasValue)
                            ?.ProductoId;
                    }
                }

                var linea = CompraNotaProveedorDetalle.Create(
                    tenantId,
                    productoId,
                    string.IsNullOrEmpty(item.CodigoPrincipal) ? null : item.CodigoPrincipal,
                    item.Descripcion,
                    item.Cantidad,
                    item.PrecioUnitario,
                    item.Subtotal,
                    item.Impuesto,
                    item.Total,
                    userId);
                nota.AgregarDetalle(linea);
            }

            await _compraRepo.AddNotaProveedorAsync(nota, ct);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _user.Email, _user.FullName,
                module: "compras", action: "notas-proveedor.importar",
                entityType: "CompraNotaProveedor", entityId: nota.Id,
                description: $"{parsed.TipoNota} {parsed.NumeroNota} — clave {parsed.ClaveAcceso}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Nota proveedor importada: id {NotaId}, tenant {TenantId}, tipo {Tipo}.",
                nota.Id, tenantId, parsed.TipoNota);

            return Result<CompraNotaProveedorDto>.Success(ToDto(nota));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al importar nota proveedor");
            return Result<CompraNotaProveedorDto>.Failure($"No se pudo importar la nota: {ex.Message}");
        }
    }

    private static CompraNotaProveedorDto ToDto(CompraNotaProveedor n) => new(
        n.Id,
        n.ProveedorId,
        n.CompraFacturaId,
        n.GastoFacturaId,
        n.TipoNota,
        n.Motivo,
        n.ClaveAcceso,
        n.FechaEmision,
        n.Establecimiento,
        n.PuntoEmision,
        n.Secuencial,
        n.Subtotal,
        n.Impuesto,
        n.Total,
        n.Estado,
        n.XmlPath,
        n.AsientoContableId,
        n.CreatedAt);
}
