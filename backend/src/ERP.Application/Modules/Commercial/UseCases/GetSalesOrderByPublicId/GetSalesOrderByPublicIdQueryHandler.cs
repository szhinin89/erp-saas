using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Commercial.Interfaces;
using ERP.Domain.Modules.Fiscal.Interfaces;
using ERP.Domain.Modules.Integration.Constants;
using ERP.Domain.Modules.Integration.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Commercial.UseCases.GetSalesOrderByPublicId;

public sealed class GetSalesOrderByPublicIdQueryHandler
    : IRequestHandler<GetSalesOrderByPublicIdQuery, Result<SalesOrderDetailDto?>>
{
    private readonly ISalesOrderRepository _orderRepo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IDocumentRelationRepository _relationRepo;
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany _currentCompany;

    public GetSalesOrderByPublicIdQueryHandler(
        ISalesOrderRepository orderRepo,
        IBusinessPartnerRepository bpRepo,
        IWarehouseRepository warehouseRepo,
        IDocumentRelationRepository relationRepo,
        IInvoiceRepository invoiceRepo,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany)
    {
        _orderRepo = orderRepo;
        _bpRepo = bpRepo;
        _warehouseRepo = warehouseRepo;
        _relationRepo = relationRepo;
        _invoiceRepo = invoiceRepo;
        _currentSubscriber = currentSubscriber;
        _currentCompany = currentCompany;
    }

    public async Task<Result<SalesOrderDetailDto?>> Handle(
        GetSalesOrderByPublicIdQuery query,
        CancellationToken ct)
    {
        if (!_currentCompany.HasCompanyContext)
            return Result<SalesOrderDetailDto?>.Failure("No hay empresa operativa seleccionada.");

        var subscriberId = _currentSubscriber.SubscriberId;
        var order = await _orderRepo.GetByPublicIdReadOnlyAsync(query.PublicId, ct);
        if (order is null || order.SubscriberId != subscriberId)
            return Result<SalesOrderDetailDto?>.Success(null);

        if (order.BranchId != _currentCompany.CompanyId)
            return Result<SalesOrderDetailDto?>.Success(null);

        var bp = await _bpRepo.GetByIdAsync(order.BusinessPartnerId, ct);
        var warehouse = await _warehouseRepo.GetByIdAsync(
            order.SubscriberId,
            order.WarehouseId,
            ct);

        Guid? relatedInvoicePublicId = null;
        var invoiceInternalId = await _relationRepo.GetTargetIdBySourceAsync(
            subscriberId,
            DocumentModules.CommercialSalesOrder,
            order.Id,
            DocumentRelationTypes.OrderToInvoice,
            ct);
        if (invoiceInternalId is > 0)
        {
            relatedInvoicePublicId = await _invoiceRepo.GetPublicIdByInternalIdAsync(
                invoiceInternalId.Value,
                subscriberId,
                ct);
        }

        return Result<SalesOrderDetailDto?>.Success(
            SalesOrderDtoMapper.ToDetail(
                order,
                bp?.Name.LegalName ?? order.BusinessPartnerId.ToString(),
                warehouse?.Name ?? order.WarehouseId.ToString(),
                relatedInvoicePublicId));
    }
}

