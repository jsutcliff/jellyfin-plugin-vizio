// Runs on Vizio SmartCast TVs only, injected into jellyfin-web's index.html before
// the app bundles load (by the plugin, or by proxy.js during development).
(function () {
    // jellyfin-web doesn't detect the Vizio browser as a TV, so it picks the desktop
    // layout, which ignores arrow keys. Force TV layout.
    try { localStorage.setItem('layout', 'tv'); } catch (e) {}

    saveLaunchLogin();

    // The plugin's launch link passes a login in the URL fragment. Store it the way
    // jellyfin-web stores a remembered login, then drop it from the URL before the
    // app reads its hash route.
    function saveLaunchLogin() {
        var match = /[#&]vizio-login=([^&]+)/.exec(location.hash);
        if (!match) return;
        history.replaceState(null, '', location.pathname + location.search);

        try {
            var login = JSON.parse(atob(decodeURIComponent(match[1])));
            var credentials = JSON.parse(localStorage.getItem('jellyfin_credentials') || '{}');
            var servers = credentials.Servers || [];
            var server = null;
            for (var i = 0; i < servers.length; i++) {
                if (servers[i].Id === login.serverId) server = servers[i];
            }
            if (!server) {
                server = { Id: login.serverId };
                servers.push(server);
            }

            server.Name = login.serverName;
            // Same address jellyfin-web uses for the server it's hosted by (origin + base url)
            server.ManualAddress = location.origin + location.pathname.replace(/\/web\/.*$/i, '');
            server.LastConnectionMode = 2; // ConnectionMode.Manual
            server.UserId = login.userId;
            server.AccessToken = login.accessToken;
            server.DateLastAccessed = new Date().getTime();
            credentials.Servers = servers;

            localStorage.setItem('jellyfin_credentials', JSON.stringify(credentials));
            localStorage.setItem('_deviceId2', login.deviceId);
            localStorage.setItem('enableAutoLogin', 'true');
        } catch (e) {}
    }

    if (!window.VIZIO_DEBUG) return;

    // The TV has no devtools, so report to the server log instead
    function log(msg) {
        try {
            var xhr = new XMLHttpRequest();
            xhr.open('POST', '../Vizio/Log');
            xhr.setRequestHeader('Content-Type', 'text/plain');
            xhr.send(String(msg));
        } catch (e) {}
    }

    log('loaded, UA: ' + navigator.userAgent);

    window.addEventListener('keydown', function (e) {
        log('keydown key=' + e.key + ' code=' + e.code + ' keyCode=' + e.keyCode);
    }, true);
})();
