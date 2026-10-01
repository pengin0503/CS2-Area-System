using System;
using System.Collections.Generic;
using System.IO;
using Colossal.IO.AssetDatabase;
namespace CS2AreaSystem.Core.Prefabs
{
    // Resolves host-less game UI paths ("Media/Game/Icons/X.svg") the same way the 1.6.2f1 UI resource handler
    // does for the page at assetdb://gameui/: every UIHostAsset registered for the "gameui" host is a root,
    // assetdb hosts are looked up in AssetDatabase.global, file hosts on disk. Only public APIs are used and the
    // shipped UI/media are never copied or modified.
    // The result per path is cached, false included (#77). In 1.6.2f1 the UI's "gameui" hosts are added only by
    // GameManager.InitializeUI, which runs before the mod manager loads any mod (pinned by GameUIResourceAvailabilityTests
    // against the shipped binaries); mods only add "ui-mods". So the host set cannot change after the first lookup here,
    // and a cached miss stays a miss for the game's own resource handler too.
    public sealed class GameUIResourceAvailability
    {
        public const string GameUIHost = "gameui";
        private readonly Dictionary<string, bool> cache = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Func<string, bool> probe;
        public GameUIResourceAvailability() : this(ProbeGameUIHosts) { }
        // Tests inject the probe; the game uses the asset database.
        public GameUIResourceAvailability(Func<string, bool> probe) => this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
        public bool Exists(string relativePath)
        {
            if (!GameUIResourcePath.IsHostless(relativePath)) return false;
            if (cache.TryGetValue(relativePath, out var known)) return known;
            bool found = probe(relativePath);
            cache[relativePath] = found;
            return found;
        }
        private static bool ProbeGameUIHosts(string relativePath)
        {
            foreach (UIHostAsset host in AssetDatabase.global.GetAssets(default(SearchFilter<UIHostAsset>)))
            {
                if (!string.Equals(host.hostname, GameUIHost, StringComparison.Ordinal)) continue;
                if (host.scheme == "assetdb")
                {
                    var root = host.uiUri;
                    if (root != null && AssetDatabase.global.TryGetAsset(GameUIResourcePath.Combine(root, relativePath), out _)) return true;
                }
                else if (!string.IsNullOrEmpty(host.uiPath) && File.Exists(Path.Combine(host.uiPath, relativePath))) return true;
            }
            return false;
        }
    }
    public static class GameUIResourcePath
    {
        public static bool IsHostless(string path) =>
            !string.IsNullOrWhiteSpace(path) && !path.Contains("://") && !path.StartsWith("/", StringComparison.Ordinal) && !path.Contains("..");
        // Mirrors DefaultResourceHandler.RequestAssetFromAssetDatabase: root scheme/host, root path + request path.
        public static Uri Combine(Uri root, string relativePath) =>
            new UriBuilder(root.Scheme, root.Host) { Path = root.AbsolutePath + relativePath }.Uri;
    }
}
