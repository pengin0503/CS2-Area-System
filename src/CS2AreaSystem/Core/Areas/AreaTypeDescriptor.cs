using System;
using CS2AreaSystem.Core.Modules;
using Game.Prefabs;
namespace CS2AreaSystem.Core.Areas
{
    public sealed class AreaTypeDescriptor
    {
        public int TypeId { get; }
        public string PrefabName { get; }
        public string DisplayNameKey { get; }
        public string IconUri { get; }
        public IAreaModule Module { get; }
        public AreaPrefab Prefab { get; internal set; }
        public AreaTypeDescriptor(int typeId, string prefabName, string displayNameKey, string iconUri, IAreaModule module = null)
        {
            if (typeId <= 0) throw new ArgumentOutOfRangeException(nameof(typeId));
            TypeId = typeId;
            PrefabName = prefabName ?? throw new ArgumentNullException(nameof(prefabName));
            DisplayNameKey = displayNameKey ?? throw new ArgumentNullException(nameof(displayNameKey));
            IconUri = iconUri;
            Module = module;
        }
    }
}
