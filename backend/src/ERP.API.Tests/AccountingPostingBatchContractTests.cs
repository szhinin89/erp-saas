using System.Reflection;
using ERP.API.Controllers.Accounting;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.UseCases.Continuity;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Tests;

public sealed class AccountingPostingBatchContractTests
{
    [Theory]
    [InlineData("ReprocessPostings", "postings/reprocess")]
    [InlineData("ReprocessPosting", "postings/{id:guid}/reprocess")]
    public void Manual_endpoints_require_existing_permission_and_body(string methodName, string route)
    {
        var method = typeof(AccountingController).GetMethod(methodName)!;
        method.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be(route);
        method.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be($"perm:{AccountingPermissions.PostingsReprocess}");
        method.GetParameters().Should().Contain(p => p.GetCustomAttribute<FromBodyAttribute>() != null);
        typeof(ICompanyScopedRequest).IsAssignableFrom(typeof(ReprocessAccountingPostingsCommand)).Should().BeTrue();
    }
}
