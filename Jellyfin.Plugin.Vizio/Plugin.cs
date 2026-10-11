using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Vizio.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Vizio;

/// <summary>
/// Makes jellyfin-web usable on Vizio SmartCast TVs and adds a one-tap launch link.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override string Name => "Vizio SmartCast";

    public override Guid Id => Guid.Parse("5431edc7-e380-4835-bce1-ff6712a61cd9");

    public override string Description => "Fixes remote navigation for Vizio SmartCast TVs and adds a link that launches Jellyfin on the TV.";

    public static Plugin? Instance { get; private set; }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            }
        ];
    }
}
