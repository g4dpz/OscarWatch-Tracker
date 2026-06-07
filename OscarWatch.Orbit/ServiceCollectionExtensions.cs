using Microsoft.Extensions.DependencyInjection;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Orbit;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOscarWatchOrbit(this IServiceCollection services)
    {
        services.AddSingleton<IOrbitPropagator, PublicOrbitToolsPropagator>();
        services.AddSingleton<IGroundGeometry>(sp =>
            new SampledGroundGeometry(sp.GetRequiredService<IOrbitPropagator>()));
        services.AddSingleton<IPassPredictor, BruteForcePassPredictor>();
        services.AddSingleton<IIlluminationPredictor>(sp =>
            new SunlightSegmentPredictor(sp.GetRequiredService<IOrbitPropagator>()));
        return services;
    }
}
