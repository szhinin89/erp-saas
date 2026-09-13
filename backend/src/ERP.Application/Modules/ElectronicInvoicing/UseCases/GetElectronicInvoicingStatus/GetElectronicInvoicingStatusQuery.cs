using ERP.Application.Common;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using MediatR;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.GetElectronicInvoicingStatus;

/// <summary>
/// ELECTRONIC-INVOICING-SRI-CONNECTIVITY-CHECK-SCOPE-01: <paramref name="CheckConnectivity"/>
/// separa el estado local de configuración (certificado, ambiente, URL — siempre resuelto) de la
/// conectividad externa real al SRI (ping al WSDL, solo cuando se pide explícitamente). Default
/// <c>true</c> para no romper el comportamiento histórico de quien instancie este query
/// directamente (incl. los tests unitarios existentes); el endpoint HTTP
/// (<see cref="ERP.API.Controllers.ElectronicInvoicingController.GetStatus"/>) es quien decide el
/// default real para el bootstrap global (<c>false</c>) vs. un check explícito (<c>true</c>).
/// </summary>
public sealed record GetElectronicInvoicingStatusQuery(bool CheckConnectivity = true)
    : IRequest<Result<ElectronicInvoicingStatusDto>>;
