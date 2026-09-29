using System.Reflection;
using ERP.API.Contracts.MasterData;
using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Domain.MasterData.Enums;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.MasterData;

/// <summary>
/// ZH-BP-NESTED-RESOURCE-OWNERSHIP-01 — cada acción de
/// <c>/business-partners/{bpId}/(roles|contacts|locations)/{childId}</c> reenvía a Application el
/// bpId REAL de la ruta junto con el childId (antes se descartaba con <c>_ = bpId</c>), y un
/// NotFound de Application sale como HTTP 404. La validación de ownership vive en los handlers
/// (ver ERP.Application.Tests/MasterData/BpNestedResourceOwnershipHandlerTests).
/// </summary>
public sealed class BusinessPartnerNestedRoutesControllerTests
{
    private static readonly Guid BpId = Guid.NewGuid();
    private static readonly Guid ChildId = Guid.NewGuid();

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "ERP.API.Tests";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static T WithContext<T>(T controller)
        where T : ControllerBase
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(new StubWebHostEnvironment());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() },
        };
        return controller;
    }

    /// <summary>Responde NotFound con el Result&lt;T&gt; que corresponda al request recibido.</summary>
    private static object NotFoundFor(object request)
    {
        var responseType = request
            .GetType()
            .GetInterfaces()
            .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
            .GetGenericArguments()[0];
        return responseType
            .GetMethod("NotFound", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, ["No encontrado."])!;
    }

    public static TheoryData<string, string> NestedActions =>
        new()
        {
            { "contacts:get", "ContactId" },
            { "contacts:update", "ContactId" },
            { "contacts:set-primary", "ContactId" },
            { "contacts:activate", "ContactId" },
            { "contacts:deactivate", "ContactId" },
            { "locations:get", "LocationId" },
            { "locations:update", "LocationId" },
            { "locations:set-primary", "LocationId" },
            { "locations:activate", "LocationId" },
            { "locations:deactivate", "LocationId" },
            { "roles:revoke", "RoleId" },
            { "roles:supplier-config", "RoleId" },
            { "roles:carrier-config", "RoleId" },
            { "roles:customer-config", "RoleId" },
            { "roles:notes", "RoleId" },
        };

    private static Task<IActionResult> Invoke(string action, IMediator mediator)
    {
        var contacts = WithContext(new BusinessPartnerContactsController(mediator));
        var locations = WithContext(new BusinessPartnerLocationsController(mediator));
        var roles = WithContext(new BusinessPartnerRolesController(mediator));
        return action switch
        {
            "contacts:get" => contacts.GetContact(BpId, ChildId),
            "contacts:update" => contacts.UpdateContact(
                BpId, ChildId, new UpdateContactRequest { FirstName = "Ana", Role = ContactRole.Purchasing }),
            "contacts:set-primary" => contacts.SetPrimary(BpId, ChildId),
            "contacts:activate" => contacts.Activate(BpId, ChildId),
            "contacts:deactivate" => contacts.Deactivate(BpId, ChildId),
            "locations:get" => locations.GetLocation(BpId, ChildId),
            "locations:update" => locations.UpdateLocation(
                BpId, ChildId, new UpdateLocationRequest { Name = "Bodega", Type = LocationType.Warehouse, AddressLine = "Calle 1" }),
            "locations:set-primary" => locations.SetPrimary(BpId, ChildId),
            "locations:activate" => locations.Activate(BpId, ChildId),
            "locations:deactivate" => locations.Deactivate(BpId, ChildId),
            "roles:revoke" => roles.RevokeRole(BpId, ChildId),
            "roles:supplier-config" => roles.UpdateSupplierConfig(BpId, ChildId, new SupplierConfigRequest()),
            "roles:carrier-config" => roles.UpdateCarrierConfig(BpId, ChildId, new CarrierConfigRequest()),
            "roles:customer-config" => roles.UpdateCustomerConfig(BpId, ChildId, new CustomerConfigRequest()),
            "roles:notes" => roles.UpdateNotes(BpId, ChildId, new UpdateRoleNotesRequest { Notes = "x" }),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    [Theory]
    [MemberData(nameof(NestedActions))]
    public async Task Accion_anidada_envia_bpId_de_la_ruta_y_childId_y_mapea_NotFound_a_404(
        string action,
        string childProperty
    )
    {
        object? sent = null;
        var mediator = new StubMediator(request =>
        {
            sent = request;
            return NotFoundFor(request);
        });

        var response = await Invoke(action, mediator);

        sent.Should().NotBeNull();
        var type = sent!.GetType();
        type.GetProperty("BusinessPartnerId")!.GetValue(sent).Should().Be(BpId, $"{action} debe propagar el bpId de la ruta");
        type.GetProperty(childProperty)!.GetValue(sent).Should().Be(ChildId);
        response.Should().BeOfType<NotFoundObjectResult>();
    }
}
