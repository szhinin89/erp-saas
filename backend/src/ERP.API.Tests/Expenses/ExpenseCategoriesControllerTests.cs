using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.UseCases.Categories;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Expenses.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Expenses;

public sealed class ExpenseCategoriesControllerTests
{
    private static ExpenseCategoriesController BuildController(Func<object, object> handler)
    {
        var controller = new ExpenseCategoriesController(new StubMediator(handler));
        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(new StubWebHostEnvironment());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider(),
            },
        };
        return controller;
    }

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

    private static ExpenseCategoryNodeDto SampleNode(Guid id) =>
        new(
            id,
            Guid.NewGuid(),
            null,
            "ADM",
            "Administrativos",
            null,
            ExpenseCategoryNodeLevel.Type,
            null,
            false,
            false,
            true
        );

    [Fact]
    public void El_controlador_exige_autenticacion()
    {
        typeof(ExpenseCategoriesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Should()
            .ContainSingle();
    }

    [Theory]
    [InlineData(nameof(ExpenseCategoriesController.GetTree), ExpensePermissions.CatalogView)]
    [InlineData(nameof(ExpenseCategoriesController.GetById), ExpensePermissions.CatalogView)]
    [InlineData(nameof(ExpenseCategoriesController.Create), ExpensePermissions.CatalogCreate)]
    [InlineData(nameof(ExpenseCategoriesController.Update), ExpensePermissions.CatalogUpdate)]
    [InlineData(nameof(ExpenseCategoriesController.Activate), ExpensePermissions.CatalogActivate)]
    [InlineData(nameof(ExpenseCategoriesController.Deactivate), ExpensePermissions.CatalogDeactivate)]
    public void Cada_endpoint_expone_su_permiso_propio(string methodName, string permission)
    {
        var method = typeof(ExpenseCategoriesController).GetMethod(methodName)!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{permission}");
    }

    [Fact]
    public async Task GetTree_exitoso_retorna_200_y_envia_includeInactive()
    {
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<IReadOnlyList<ExpenseCategoryTreeNodeDto>>.Success(
                new List<ExpenseCategoryTreeNodeDto>()
            );
        });

        var response = await controller.GetTree(true, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sent.Should().BeOfType<ListExpenseCategoryTreeQuery>();
        ((ListExpenseCategoryTreeQuery)sent!).IncludeInactive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_exitoso_retorna_201_y_envia_el_command_recibido()
    {
        object? sent = null;
        var id = Guid.NewGuid();
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<ExpenseCategoryNodeDto>.Success(SampleNode(id));
        });
        var command = new CreateExpenseCategoryNodeCommand(
            "ADM",
            "Administrativos",
            ExpenseCategoryNodeLevel.Type,
            null,
            null
        );

        var response = await controller.Create(command, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        sent.Should().Be(command);
    }

    [Fact]
    public async Task Update_exitoso_retorna_200_y_envia_el_id_de_ruta()
    {
        object? sent = null;
        var id = Guid.NewGuid();
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<ExpenseCategoryNodeDto>.Success(SampleNode(id));
        });

        var response = await controller.Update(
            id,
            new UpdateExpenseCategoryNodeRequest("ADM", "Administrativos", null, "Notas"),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sent
            .Should()
            .BeEquivalentTo(
                new UpdateExpenseCategoryNodeCommand(id, "ADM", "Administrativos", null, "Notas")
            );
    }

    [Fact]
    public async Task Deactivate_con_hijos_activos_retorna_422()
    {
        var controller = BuildController(_ =>
            Result<ExpenseCategoryNodeDto>.ValidationFailure(
                "No se puede desactivar el nodo porque tiene hijos activos."
            )
        );

        var response = await controller.Deactivate(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public void El_controlador_no_expone_DELETE()
    {
        var methods = typeof(ExpenseCategoriesController).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
        );

        methods
            .SelectMany(m =>
                m.GetCustomAttributes(
                    typeof(Microsoft.AspNetCore.Mvc.HttpDeleteAttribute),
                    inherit: true
                )
            )
            .Should()
            .BeEmpty();
    }
}
