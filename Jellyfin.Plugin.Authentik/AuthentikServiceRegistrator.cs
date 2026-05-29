using Jellyfin.Plugin.Authentik.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Authentik;

/// <summary>
/// Registers plugin services with the Jellyfin DI container.
/// </summary>
public class AuthentikServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<OidcService>();
        serviceCollection.AddSingleton<UserSyncService>();
    }
}
