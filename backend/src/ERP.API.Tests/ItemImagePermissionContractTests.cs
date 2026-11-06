using ERP.API.Controllers;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Tests;

public sealed class ItemImagePermissionContractTests
{
    [Theory]
    [InlineData(nameof(ItemImagesController.EnableImage))]
    [InlineData(nameof(ItemImagesController.DisableImage))]
    [InlineData(nameof(ItemImagesController.ReplaceImages))]
    public void Image_writes_require_existing_edit_permission(string name)
    {
        typeof(ItemImagesController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Should().NotBeEmpty();
        var method = typeof(ItemImagesController).GetMethod(name)!;
        method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().Contain(x => x.Policy == $"perm:{InventoryPermissions.ItemsEdit}");
    }

    [Fact]
    public void Enable_uses_approved_patch_route()
    {
        typeof(ItemImagesController).GetMethod(nameof(ItemImagesController.EnableImage))!
            .GetCustomAttributes(typeof(HttpPatchAttribute), true).Cast<HttpPatchAttribute>().Single().Template
            .Should().Be("{id:guid}/images/{imageId:guid}/enable");
    }
}
