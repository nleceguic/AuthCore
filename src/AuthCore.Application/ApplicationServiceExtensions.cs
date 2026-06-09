using AuthCore.Application.Interfaces;
using AuthCore.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AuthCore.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
