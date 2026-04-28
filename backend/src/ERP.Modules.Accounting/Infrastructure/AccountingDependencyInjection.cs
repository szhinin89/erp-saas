using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Accounting.Application.Interfaces;
using Modules.Accounting.Application.UseCases;
using Modules.Accounting.Infrastructure.Persistence;
using Modules.Accounting.Infrastructure.Repositories;

namespace Modules.Accounting.Infrastructure;

public static class AccountingDependencyInjection
{
    public static IServiceCollection AddAccountingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AccountingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<GetAllAccountsUseCase>();
        services.AddScoped<CreateAccountUseCase>();

        return services;
    }
}
