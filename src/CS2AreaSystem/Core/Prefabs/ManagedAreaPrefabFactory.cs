using System;
using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using Game.Prefabs;
using UnityEngine;
namespace CS2AreaSystem.Core.Prefabs
{
    public sealed class AreaPrefabMetadata
    {
        public string Name { get; }
        public string IconUri { get; }
        public int TypeId { get; }
        public Game.Areas.AreaType GeometryType => ManagedAreaPrefabFactory.NativeGeometryType;
        public bool AllowOverlap => true;
        public bool AllowEditing => true;
        public AreaPrefabMetadata(AreaTypeDescriptor descriptor, string icon) { Name = descriptor.PrefabName; TypeId = descriptor.TypeId; IconUri = icon; }
    }
    public static class AreaPrefabReadiness
    {
        public static bool CanUse(bool entityExists, bool archetypeValid, bool isManagementGeometry) => entityExists && archetypeValid && isManagementGeometry;
    }
    public sealed class ManagedAreaPrefabFactory
    {
        public static Game.Areas.AreaType NativeGeometryType => Game.Areas.AreaType.Space;
        // Vanilla 1.6.2f1 icons are host-less paths resolved by the game UI; coui://gameui is not a registered host.
        public const string FallbackIconUri = "Media/Game/Icons/SurfaceAreaTool.svg";
        public static AreaTypeDescriptor Describe(AreaTypeId kind)
        {
            switch (kind)
            {
                case AreaTypeId.Park: return new AreaTypeDescriptor(1, "CS2AS.ParkArea", "CS2AS.Type.Park", "Media/Game/Icons/LargeParks.svg");
                case AreaTypeId.Industrial: return new AreaTypeDescriptor(2, "CS2AS.IndustrialArea", "CS2AS.Type.Industrial", "Media/Game/Icons/ZoneIndustrial.svg");
                case AreaTypeId.Tourism: return new AreaTypeDescriptor(3, "CS2AS.TourismArea", "CS2AS.Type.Tourism", "Media/Game/Icons/Attraction.svg");
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        public static AreaPrefabMetadata Map(AreaTypeDescriptor descriptor, Func<string, bool> iconAvailable)
        {
            var preferred = descriptor.IconUri;
            return new AreaPrefabMetadata(descriptor, !string.IsNullOrWhiteSpace(preferred) && iconAvailable(preferred) ? preferred : FallbackIconUri);
        }
        // Availability comes from the running game (GameUIResourceAvailability); tests inject it.
        // Fall back only when the fallback is verifiably present: if neither resource can be verified the lookup
        // itself is unreliable, and the preferred vanilla icon is kept rather than degrading every area type.
        public static string ResolveIcon(AreaTypeDescriptor descriptor, Func<string, bool> iconAvailable)
        {
            var preferred = descriptor.IconUri;
            if (string.IsNullOrWhiteSpace(preferred) || iconAvailable(preferred) || !iconAvailable(FallbackIconUri))
                return string.IsNullOrWhiteSpace(preferred) ? FallbackIconUri : preferred;
            return FallbackIconUri;
        }
        public SpacePrefab CreateAreaPrefab(AreaTypeDescriptor descriptor, string iconUri)
        {
            // Adapted from KruemelGames/ParkingLotTool, Tools/ParkingLotLotArea.cs,
            // TryResolveLotOwnerPrefab: display colors for a non-clearing area boundary.
            // SpaceData is gameplay-creatable; LotData is editor-only in 1.6.2f1.
            var lot = ScriptableObject.CreateInstance<SpacePrefab>();
            lot.name = descriptor.PrefabName;
            var color = descriptor.TypeId == 1 ? new Color(0.20f, 0.65f, 0.30f, 1f) :
                descriptor.TypeId == 2 ? new Color(0.90f, 0.65f, 0.15f, 1f) : new Color(0.30f, 0.55f, 0.95f, 1f);
            lot.m_Color = new Color(color.r, color.g, color.b, 0.04f);
            lot.m_EdgeColor = new Color(color.r, color.g, color.b, 0.30f);
            lot.m_SelectionColor = new Color(color.r, color.g, color.b, 0.16f);
            lot.m_SelectionEdgeColor = color;
            lot.AddComponent<UIObject>().m_Icon = string.IsNullOrWhiteSpace(iconUri) ? FallbackIconUri : iconUri;
            return lot;
        }
    }
}
