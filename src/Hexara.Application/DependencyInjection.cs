using Hexara.Application.Common.Interfaces;
using Hexara.Application.Games;
using Hexara.Application.Rooms;
using Microsoft.Extensions.DependencyInjection;

namespace Hexara.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddHexaraApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<GameService>();
        services.AddScoped<RoomService>();
        return services;
    }
}
