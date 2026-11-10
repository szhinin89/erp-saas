using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Application.Modules.OrgConfig.DTOs;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Pricing.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.OrgConfig.UseCases.UpsertCompanyInvoiceOrgSettings;

/// <summary>
/// SALES-PREFERENCES-INTEGRITY-01: guardado atómico de Configuración → Ventas. Los tres OrgSettings
/// y, si se pide, la reasignación de <c>PriceList.IsDefault</c> (vía
/// <see cref="IPriceListDefaultAssigner"/>, la misma lógica del PATCH <c>set-default</c>) comparten
/// el DbContext y se confirman en una única transacción (<see cref="IUnitOfWork.ExecuteInTransactionAsync"/>):
/// cualquier fallo revierte el conjunto completo. La autorización de
/// Pricing y la validez de la lista se comprueban antes de mutar cualquier dato.
/// </summary>
public sealed class UpsertCompanyInvoiceOrgSettingsCommandHandler
    : IRequestHandler<UpsertCompanyInvoiceOrgSettingsCommand, Result<CompanyInvoiceOrgSettingsDto>>
{
    private readonly IOrgSettingsRepository _repo;
    private readonly IPriceListRepository _priceLists;
    private readonly IPriceListDefaultAssigner _defaultAssigner;
    private readonly IRuntimePermissionAuthorizer _authorizer;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInvoiceDefaultsCatalogValidator _catalog;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;

    public UpsertCompanyInvoiceOrgSettingsCommandHandler(
        IOrgSettingsRepository repo,
        IPriceListRepository priceLists,
        IPriceListDefaultAssigner defaultAssigner,
        IRuntimePermissionAuthorizer authorizer,
        IUnitOfWork unitOfWork,
        IInvoiceDefaultsCatalogValidator catalog,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser
    )
    {
        _repo = repo;
        _priceLists = priceLists;
        _defaultAssigner = defaultAssigner;
        _authorizer = authorizer;
        _unitOfWork = unitOfWork;
        _catalog = catalog;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
    }

    public async Task<Result<CompanyInvoiceOrgSettingsDto>> Handle(
        UpsertCompanyInvoiceOrgSettingsCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;
        var userId = _currentUser.UserId;

        var docTypeCode = Normalize(command.DefaultDocTypeCode);
        var paymentMethodCode = Normalize(command.DefaultSriPaymentMethodCode);

        // Pedir un cambio de lista exige además el permiso de Pricing (el mismo del PATCH
        // set-default). Se rechaza la operación completa antes de escribir nada.
        if (
            command.DefaultPriceList is not null
            && !await _authorizer.IsAuthorizedAsync(
                PricingPermissions.Update,
                userId,
                _currentUser.Role ?? string.Empty,
                cancellationToken
            )
        )
            return Result<CompanyInvoiceOrgSettingsDto>.Forbidden(
                "No tiene permiso para cambiar la lista de precios predeterminada."
            );

        Guid? defaultPriceListId = null;
        Result<CompanyInvoiceOrgSettingsDto>? rejection = null;

        // Una sola transacción: la reasignación de PriceList.IsDefault, los tres OrgSettings y su
        // historial se confirman juntos; cualquier excepción hace rollback completo. Un rechazo
        // de la lista ocurre antes de mutar nada, así que la transacción termina vacía.
        await _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                if (command.DefaultPriceList is { } priceListChange)
                {
                    var assignment = await _defaultAssigner.AssignAsync(
                        tenantId,
                        priceListChange.PriceListId,
                        userId,
                        ct
                    );
                    rejection = assignment.Error switch
                    {
                        PriceListDefaultAssignmentError.NotFound =>
                            Result<CompanyInvoiceOrgSettingsDto>.NotFound(
                                PriceListDefaultMessages.NotFound
                            ),
                        PriceListDefaultAssignmentError.Inactive =>
                            Result<CompanyInvoiceOrgSettingsDto>.ValidationFailure(
                                PriceListDefaultMessages.Inactive
                            ),
                        _ => null,
                    };
                    if (rejection is not null)
                        return;
                    defaultPriceListId = assignment.Default?.Id;
                }
                else
                {
                    defaultPriceListId = (await _priceLists.GetDefaultAsync(tenantId, ct))?.Id;
                }

                await UpsertAsync(
                    tenantId,
                    companyId,
                    OrgSettingKeys.Invoice.DefaultDocTypeCode,
                    docTypeCode,
                    SettingDataType.String,
                    userId,
                    ct
                );
                await UpsertAsync(
                    tenantId,
                    companyId,
                    OrgSettingKeys.Invoice.DefaultPaymentMethodCode,
                    paymentMethodCode,
                    SettingDataType.String,
                    userId,
                    ct
                );
                await UpsertAsync(
                    tenantId,
                    companyId,
                    OrgSettingKeys.Invoice.DefaultPaymentTermId,
                    command.DefaultPaymentTermId?.ToString(),
                    SettingDataType.Guid,
                    userId,
                    ct
                );
            },
            cancellationToken
        );

        if (rejection is not null)
            return rejection;

        return Result<CompanyInvoiceOrgSettingsDto>.Success(
            new CompanyInvoiceOrgSettingsDto(
                docTypeCode,
                paymentMethodCode,
                command.DefaultPaymentTermId,
                defaultPriceListId,
                Array.Empty<string>(),
                await _catalog.GetAllowedDocTypeCodesAsync(cancellationToken)
            )
        );
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task UpsertAsync(
        Guid tenantId,
        Guid companyId,
        string key,
        string? value,
        SettingDataType dataType,
        Guid userId,
        CancellationToken ct
    )
    {
        var setting = OrgSetting.Create(
            tenantId,
            companyId,
            OrgScope.Company,
            companyId,
            key,
            value,
            dataType,
            userId
        );

        await _repo.UpsertAsync(setting, ct);
    }
}
