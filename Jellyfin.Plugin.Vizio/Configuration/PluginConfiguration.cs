using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Vizio.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the TV's IP address (or hostname).
    /// </summary>
    public string TvAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the auth token the TV issued when pairing.
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the device id we pair as. Generated on first pairing.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL the TV should open. Empty means derive it from the
    /// address used to request the launch link.
    /// </summary>
    public string LaunchUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user the TV is logged in as when launched. Empty means the
    /// TV shows the normal login screen.
    /// </summary>
    public string TvUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the TV reports key presses to the server log.
    /// </summary>
    public bool DebugLogging { get; set; }
}
