using System.Net.Http.Headers;
using System.Reflection;
using OscarWatch.Core.Services;

namespace OscarWatch.Core.Net;

public static class OscarWatchHttpClients
{
    public const string ProductName = "OscarWatch";

    private static readonly SocketsHttpHandler SharedHandler = new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 10
    };

    public static ProductInfoHeaderValue CreateUserAgent() =>
        new(ProductName, GetProductVersion());

    public static void ApplyUserAgent(HttpClient client)
    {
        if (client.DefaultRequestHeaders.UserAgent.Count > 0)
            return;

        client.DefaultRequestHeaders.UserAgent.Add(CreateUserAgent());
    }

    public static HttpClient Create(TimeSpan timeout)
    {
        var client = new HttpClient(SharedHandler, disposeHandler: false)
        {
            Timeout = timeout
        };
        ApplyUserAgent(client);
        return client;
    }

    public static void DisposeSharedHandler() => SharedHandler.Dispose();

    public static string GetProductVersion()
    {
        foreach (var assembly in new[] { Assembly.GetEntryAssembly(), Assembly.GetExecutingAssembly() })
        {
            if (assembly is null)
                continue;

            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            var parsed = ReleaseVersion.TryParseVersionString(informational);
            if (parsed is not null)
                return parsed.ToString(3);

            var version = assembly.GetName().Version;
            if (version is not null && (version.Major > 0 || version.Minor > 0 || version.Build > 0))
                return version.ToString(3);
        }

        return "dev";
    }
}
