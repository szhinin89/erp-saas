using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc.Testing;

namespace ERP.API.Tests.Support;

internal static class RefreshTokenHttpTestSupport
{
    public const string CookieName = "erp_refresh_token";

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<(string RawToken, IdentityUser User, Guid SubscriberId)> CreateRefreshTokenAsync(
        IntegrationTestWebAppFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var refresh = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        var (user, subscriberId) = await SeedIdentityUserAsync(db, factory);
        var (rawToken, _) = await refresh.CreateAsync(user.Id, subscriberId, null, RefreshUserType.Identity);
        return (rawToken, user, subscriberId);
    }

    public static async Task<string> CreateSecondRefreshTokenAsync(
        IntegrationTestWebAppFactory factory,
        Guid userId,
        Guid subscriberId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var refresh = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var (rawToken, _) = await refresh.CreateAsync(userId, subscriberId, null, RefreshUserType.Identity);
        return rawToken;
    }

    public static HttpClient CreateAnonymousClient(IntegrationTestWebAppFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    public static Task<HttpResponseMessage> PostRefreshAsync(
        HttpClient client,
        RefreshHttpRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");

        if (request.SendEmptyJsonBody)
            message.Content = JsonContent.Create(new { }, options: JsonOpts);

        if (request.BodyToken is not null)
            message.Content = JsonContent.Create(new { refreshToken = request.BodyToken }, options: JsonOpts);

        if (request.CookieToken is not null)
            message.Headers.Add("Cookie", $"{CookieName}={request.CookieToken}");

        return client.SendAsync(message);
    }

    public static async Task ExpireRefreshTokenInDatabaseAsync(
        IntegrationTestWebAppFactory factory,
        string rawToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var hash = RefreshTokenService.Hash(rawToken);
        var entity = await db.RefreshTokens.SingleAsync(t => t.TokenHash == hash);
        db.Entry(entity).Property("ExpiresAt").CurrentValue = DateTime.UtcNow.AddMinutes(-10);
        await db.SaveChangesAsync();
    }

    public static async Task BackdateRevokedRotationAsync(
        IntegrationTestWebAppFactory factory,
        string rawToken,
        TimeSpan age)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var hash = RefreshTokenService.Hash(rawToken);
        var entity = await db.RefreshTokens.SingleAsync(t => t.TokenHash == hash);
        db.Entry(entity).Property(nameof(ERP.Domain.Auth.Entities.RefreshToken.RevokedAt)).CurrentValue =
            DateTime.UtcNow.Subtract(age);
        await db.SaveChangesAsync();
    }

    private static async Task<(IdentityUser User, Guid SubscriberId)> SeedIdentityUserAsync(
        ErpDbContext db,
        IntegrationTestWebAppFactory factory)
    {
        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        var subscriberId = seed.SubscriberId;
        var actorId = seed.UserId;

        var identityUser = IdentityUser.Create(
            "Test", "RefreshMatrix", "test.refresh.matrix@erp.dev", "hashed-password", actorId);
        await db.IdentityUsers.AddAsync(identityUser, CancellationToken.None);

        var defaultCompany = await db.Companies.FirstOrDefaultAsync(c => c.SubscriberId == subscriberId);
        if (defaultCompany is null)
        {
            defaultCompany = Company.CreateFromSubscriber(
                subscriberId, "1799999999001", "Refresh Matrix Co", "Seed Address");
            await db.Companies.AddAsync(defaultCompany, CancellationToken.None);
        }

        var membership = CompanyUserMembership.Create(
            defaultCompany.Id, identityUser.Id, "Admin", null, actorId);
        await db.CompanyUserMemberships.AddAsync(membership, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        factory.MutableUser.UserId = identityUser.Id;
        return (identityUser, subscriberId);
    }
}

internal sealed record RefreshHttpRequest
{
    public string? BodyToken { get; init; }
    public string? CookieToken { get; init; }
    public bool SendEmptyJsonBody { get; init; }
}
