using Microsoft.Extensions.DependencyInjection;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Orbit;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOscarWatchOrbit(this IServiceCollection services)
    {
        services.AddSingleton<IOrbitPropagator, PublicOrbitToolsPropagator>();
        services.AddSingleton<IGroundGeometry, SampledGroundGeometry>();
        services.AddSingleton<IPassPredictor, BruteForcePassPredictor>();
        return services;
    }
}
