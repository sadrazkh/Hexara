using Hexara.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Hexara.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddHexaraApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}
