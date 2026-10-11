# Jellyfin for Vizio SmartCast

A Jellyfin server plugin that makes the Jellyfin web client usable on Vizio SmartCast TVs.

- **Remote navigation.** jellyfin-web doesn't recognise the SmartCast browser as a TV, so it uses the desktop layout and ignores arrow keys. The plugin injects a small script into the web client, for SmartCast browsers only, that switches it to TV layout.
- **One-tap launch.** Pair the plugin with your TV once, then open `http://<server>/Vizio/Launch` on your phone (bookmark it) to open Jellyfin on the TV. Only answers requests from your local network.
- **Automatic sign-in.** Optionally, the launch link signs the TV in as a chosen user (the same way Quick Connect does), so there's no login screen even if the TV clears its storage.

Nothing on the server's disk is modified; the script is added to `index.html` as it's served.

Requires Jellyfin 12.0.

## Install

1. Build and serve a local plugin repository:
   ```
   ./release.sh 0.2.0.0 "changelog" http://<this-machine>:8098
   node serve-dist.js
   ```
2. Jellyfin Dashboard → Plugins → Repositories → add `http://<this-machine>:8098/manifest.json`.
3. Catalog → **Vizio SmartCast** → Install, then restart Jellyfin.
4. Dashboard → Plugins → **Vizio SmartCast**: enter the TV's IP, press **Start pairing**, and enter the PIN shown on the TV. Optionally pick a user to sign the TV in as.

## Development

`proxy.js` serves a Jellyfin server's web UI through your machine and injects `Jellyfin.Plugin.Vizio/Web/vizio.js`, re-reading it on every page load, so the script can be iterated on without rebuilding the plugin. Key presses on the TV are printed to the terminal.

```
JELLYFIN_URL=http://<server>:8096 node proxy.js
```

Then launch `http://<this-machine>:8097/web/` on the TV with Vizio's [launcher page](http://vizio-pm.s3-us-west-1.amazonaws.com/conjure-launcher.html).

Build: `dotnet build Jellyfin.Plugin.Vizio` (.NET 10 SDK). Release: `./release.sh <version> "<changelog>"`.
