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
    public sealed class GameUIResourceAvailability
    {
        public const string GameUIHost = "gameui";
        private readonly Dictionary<string, bool> cache = new Dictionary<string, bool>(StringComparer.Ordinal);
        public bool Exists(string relativePath)
        {
            if (!GameUIResourcePath.IsHostless(relativePath)) return false;
            if (cache.TryGetValue(relativePath, out var known)) return known;
            bool found = false;
            foreach (UIHostAsset host in AssetDatabase.global.GetAssets(default(SearchFilter<UIHostAsset>)))
            {
                if (!string.Equals(host.hostname, GameUIHost, StringComparison.Ordinal)) continue;
                if (host.scheme == "assetdb")
                {
                    var root = host.uiUri;
                    if (root != null && AssetDatabase.global.TryGetAsset(GameUIResourcePath.Combine(root, relativePath), out _)) { found = true; break; }
                }
                else if (!string.IsNullOrEmpty(host.uiPath) && File.Exists(Path.Combine(host.uiPath, relativePath))) { found = true; break; }
            }
            cache[relativePath] = found;
            return found;
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
