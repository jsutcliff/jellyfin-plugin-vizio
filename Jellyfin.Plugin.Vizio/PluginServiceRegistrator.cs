using System;
using Jellyfin.Plugin.Vizio.Web;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Vizio;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddTransient<IStartupFilter, IndexInjectionStartupFilter>();
        serviceCollection.AddSingleton<VizioTvClient>();
    }

    private sealed class IndexInjectionStartupFilter : IStartupFilter
    {
        // Put our middleware ahead of Jellyfin's pipeline, so it sees index.html on the way out
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.UseMiddleware<IndexInjectionMiddleware>();
                next(app);
            };
        }
    }
}
