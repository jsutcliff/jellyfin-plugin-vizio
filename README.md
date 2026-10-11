# Jellyfin for Vizio SmartCast

A Jellyfin server plugin that makes the Jellyfin web client usable on Vizio SmartCast TVs, with a one-tap way to open it from your phone.

There's no Jellyfin app in Vizio's app store, but SmartCast TVs have a built-in browser that can open a web page on request. This plugin makes Jellyfin's web client work well in that browser:

- **Remote navigation.** Out of the box, Jellyfin doesn't recognise the SmartCast browser as a TV, so it shows the desktop layout and ignores the arrow keys. The plugin switches it to the TV layout for SmartCast browsers only. Nothing changes for other devices.
- **One-tap launch.** Pair the plugin with your TV once. After that, opening a link on your phone (bookmark it) opens Jellyfin on the TV.
- **Automatic sign-in (optional).** The launch link can sign the TV in as a user you choose, so you never type a password with the remote.

The plugin doesn't modify any files on your server. It adds a small script to Jellyfin's web page as the page is sent to the TV.

## Requirements

- Jellyfin server **12.0**
- A Vizio SmartCast TV on the same network as your Jellyfin server
- Your Jellyfin server must be able to connect to the TV (HTTPS, port 7345). This is normally the case on a home network, including when Jellyfin runs in Docker.

## Install

1. In Jellyfin, go to **Dashboard → Plugins → Repositories** and add this repository URL:
   ```
   https://raw.githubusercontent.com/jsutcliff/jellyfin-plugin-vizio/main/manifest.json
   ```
2. Go to **Catalog**, find **Vizio SmartCast**, and install it.
3. Restart Jellyfin (**Dashboard → Restart**).

## Set up

1. Turn the TV on. Find its IP address in the TV's network settings.
2. In Jellyfin, open **Dashboard → Plugins → Vizio SmartCast**.
3. Enter the TV's IP address and press **Start pairing**. A PIN appears on the TV.
4. Enter the PIN and press **Pair**.
5. Optional: under **Log the TV in as**, choose a user, then press **Save**.
6. Open the **launch link** shown on the settings page on your phone and bookmark it. It looks like `http://<your-server>:8096/Vizio/Launch`.

Opening the launch link starts Jellyfin on the TV. Use the remote's arrow keys and OK button to get around.

If you don't use automatic sign-in, log in on the TV with **Remember me** ticked. Whether that login survives closing the app depends on the TV, so automatic sign-in is the more reliable option.

## Good to know

- **Jellyfin won't appear on the SmartCast home screen.** That requires Vizio's partner program. You start it with the launch link.
- **The TV must be on** when you use the launch link.
- **The launch link only works on your local network**, and it doesn't require a Jellyfin login, so it works from any phone on your Wi-Fi. With automatic sign-in on, anyone on your network who opens it can start the TV signed in as the chosen user. The login itself goes only to the TV and is never shown to the person who opened the link.
- **Some videos may be converted on the fly** if the TV can't play their format directly. That's normal Jellyfin behaviour on low-powered devices.

## Troubleshooting

- **"Couldn't reach the TV"**: check the TV is on and its IP address hasn't changed. Giving the TV a fixed IP in your router stops it changing. If Jellyfin runs in a container, make sure the container can connect to devices on your home network.
- **Pairing fails**: press **Start pairing** again to get a new PIN, and enter it promptly.
- **A remote button doesn't work**: turn on **Debug logging** in the plugin settings, press the button, and look for `[tv] keydown` lines in the Jellyfin server log (**Dashboard → Logs**). Please open an issue with those lines.

## Development

Build with the .NET 10 SDK:

```
dotnet build Jellyfin.Plugin.Vizio
```

`proxy.js` (Node.js, no dependencies) lets you work on the TV script, `Jellyfin.Plugin.Vizio/Web/vizio.js`, without rebuilding the plugin. It runs on your computer and serves your Jellyfin server's web client with the script added. The script is re-read on every page load, and key presses on the TV are printed to the terminal.

```
JELLYFIN_URL=http://<jellyfin-server>:8096 node proxy.js
```

Then open `http://<your-computer>:8097/web/` on the TV using Vizio's [launcher page](http://vizio-pm.s3-us-west-1.amazonaws.com/conjure-launcher.html) in Chrome.

### Releasing

`./release.sh <version> "<changelog>"` builds the plugin, publishes a GitHub release, and updates `manifest.json`.

To test an install without publishing, give it a URL on your machine. It writes a local manifest to `dist/`, which `serve-dist.js` serves:

```
./release.sh 0.2.0.0 "changelog" http://<your-computer>:8098
node serve-dist.js
```

Then add `http://<your-computer>:8098/manifest.json` as a repository in Jellyfin.

## License

[GPL-3.0](LICENSE), the same license as Jellyfin.
