using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Company.UseCases.UpdateCompanyForAdminCore;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Company.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Auth;

public sealed class AdminCoreCompanyUpdateControllerTests
{
    private static AdminCoreController Build(Func<object, object> handler) => new(new StubMediator(handler))
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton<IWebHostEnvironment>(new StubWebHostEnvironment()).BuildServiceProvider() }
        }
    };

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "ERP.API.Tests";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } =
            null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            null!;
    }

    [Fact]
    public void Update_requires_platform_admin_policy()
    {
        typeof(AdminCoreController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single().Policy.Should().Be("PlatformAdmin");
        typeof(AdminCoreController).GetMethod(nameof(AdminCoreController.UpdateCompany))!
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Should().BeEmpty();
    }

    [Fact]
    public async Task Route_mismatch_is_rejected_before_dispatch()
    {
        var controller = Build(_ => throw new InvalidOperationException("Must not dispatch"));
        var result = await controller.UpdateCompany(Guid.NewGuid(),
            new(Guid.NewGuid(), "Empresa", null, true, null), CancellationToken.None);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData(false, 400)]
    [InlineData(true, 409)]
    public async Task Invalid_and_duplicate_RUC_return_client_errors(bool duplicate, int status)
    {
        var controller = Build(_ => Result<CompanyDetailDto>.Failure("RUC rechazado",
            duplicate ? CompanyRucAlreadyExistsException.ErrorCode : null));
        var id = Guid.NewGuid();
        var result = await controller.UpdateCompany(id, new(id, "Empresa", null, true, "123"), CancellationToken.None);
        ((ObjectResult)result).StatusCode.Should().Be(status);
    }

    [Fact]
    public async Task Successful_update_returns_existing_company()
    {
        var entity = Company.CreateManaged(Guid.NewGuid(), "1790016919001", "Actualizada");
        var command = new UpdateCompanyForAdminCoreCommand(entity.Id, entity.LegalName, null, true, entity.TaxIdentificationNumber);
        var controller = Build(request => {
            request.Should().Be(command);
            return Result<CompanyDetailDto>.Success(CompanyDetailDto.FromEntity(entity));
        });
        var result = await controller.UpdateCompany(entity.Id, command, CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }
}
