using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.UI;
using Game;
using Game.Areas;
using Game.Common;
using Game.Prefabs;
using Game.Rendering;
using Game.SceneFlow;
using Game.Tools;
using Game.UI;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Core.Labels
{
    // District-style name labels for managed Areas. Vanilla draws labels only for area types whose AreaTypePrefab has a
    // name material (districts) and picks them with a District-only query, so this system repeats both for Spaces:
    // AreaBufferSystem's TextMeshPro layout and vertex format with the district name material, and RaycastLabelsJob's
    // label math for clicks. It runs after DefaultToolSystem in ToolUpdate, so a click it accepts replaces the selection
    // vanilla made in the same frame.
    public partial class AreaLabelSystem : GameSystemBase
    {
        private const float BoundsMargin = 2000f;
        private AreaManagementUISystem management;
        private NameSystem names;
        private ToolSystem tools;
        private DefaultToolSystem defaultTool;
        private ToolRaycastSystem toolRaycast;
        private CameraUpdateSystem cameras;
        private OverlayRenderSystem overlay;
        private RenderingSystem rendering;
        private PrefabSystem prefabs;
        private Game.Audio.AudioManager audio;
        private EntityQuery labelledAreas, areaTypes, districtNames, soundSettings;
        private readonly Dictionary<string, AreaLabelLayout> layouts = new Dictionary<string, AreaLabelLayout>(StringComparer.Ordinal);
        private readonly List<Material> materials = new List<Material>();
        private readonly List<(Entity area, float3 position, IReadOnlyList<Bounds2> lines)> targets = new List<(Entity, float3, IReadOnlyList<Bounds2>)>();
        private List<AreaLabelEntry> shown;
        private Material nameMaterial;
        private AreaNameData colors = AreaNameData.GetDefaults();
        private bool resolved, materialsChanged, failed;
        private Mesh mesh;
        private bool[] drawSubMesh = new bool[0];
        public Entity Hovered { get; private set; }
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            management = World.GetOrCreateSystemManaged<AreaManagementUISystem>();
            names = World.GetOrCreateSystemManaged<NameSystem>();
            tools = World.GetOrCreateSystemManaged<ToolSystem>();
            defaultTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            toolRaycast = World.GetOrCreateSystemManaged<ToolRaycastSystem>();
            cameras = World.GetOrCreateSystemManaged<CameraUpdateSystem>();
            overlay = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            rendering = World.GetOrCreateSystemManaged<RenderingSystem>();
            prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            audio = World.GetOrCreateSystemManaged<Game.Audio.AudioManager>();
            labelledAreas = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<ManagedArea>(), ComponentType.ReadOnly<Geometry>() },
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Hidden>(), ComponentType.ReadOnly<Deleted>() }
            });
            areaTypes = GetEntityQuery(ComponentType.ReadOnly<AreaTypeData>(), ComponentType.ReadOnly<PrefabData>());
            districtNames = GetEntityQuery(ComponentType.ReadOnly<DistrictData>(), ComponentType.ReadOnly<AreaNameData>());
            soundSettings = GetEntityQuery(ComponentType.ReadOnly<ToolUXSoundSettingsData>());
            RenderPipelineManager.beginContextRendering += Render;
        }
        [Preserve]
        protected override void OnDestroy()
        {
            RenderPipelineManager.beginContextRendering -= Render;
            if (mesh != null) UnityEngine.Object.Destroy(mesh);
            foreach (var material in materials) if (material != null) UnityEngine.Object.Destroy(material);
            materials.Clear();
            base.OnDestroy();
        }
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            Clear();
            // Prefabs are reloaded with the game; find the district name material again and rebuild every material and layout.
            resolved = false; failed = false; nameMaterial = null;
            foreach (var material in materials) if (material != null) UnityEngine.Object.Destroy(material);
            materials.Clear();
            layouts.Clear();
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (failed) return;
            try
            {
                if (!GameManager.instance.gameMode.IsGame() || !management.IsReady || !Resolve()) { Clear(); return; }
                var entries = Collect();
                Pick();
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    bool highlighted = entry.Area == Hovered || entry.Area == tools.selected;
                    if (highlighted) entries[i] = new AreaLabelEntry(entry.Area, entry.Name, entry.Position, true);
                }
                if (materialsChanged || !AreaLabelEntry.Equivalent(shown, entries)) Rebuild(entries);
            }
            catch (Exception error)
            {
                // A label failure must not break the tool loop every frame; Area Core and the panel keep working.
                failed = true;
                Clear();
                Mod.Log.Error(error, "Area labels disabled until the next game load");
            }
        }
        // The district AreaTypePrefab name material and the district label colours; without them labels stay off.
        private bool Resolve()
        {
            if (resolved) return nameMaterial != null;
            using (var types = areaTypes.ToEntityArray(Allocator.Temp))
                foreach (var entity in types)
                    if (prefabs.TryGetPrefab<AreaTypePrefab>(entity, out var prefab) && prefab.m_Type == AreaType.District && prefab.m_NameMaterial != null)
                    { nameMaterial = prefab.m_NameMaterial; break; }
            resolved = true;
            if (nameMaterial == null) { Mod.Log.Warn("District name material not found; Area map labels are off for this game."); return false; }
            using (var districts = districtNames.ToComponentDataArray<AreaNameData>(Allocator.Temp))
                colors = districts.Length > 0 ? districts[0] : AreaNameData.GetDefaults();
            return true;
        }
        private List<AreaLabelEntry> Collect()
        {
            var entries = new List<AreaLabelEntry>();
            targets.Clear();
            using (var areas = labelledAreas.ToEntityArray(Allocator.Temp))
            {
                var sorted = areas.ToArray();
                Array.Sort(sorted, (a, b) => a.Index.CompareTo(b.Index));
                foreach (var area in sorted)
                {
                    var name = names.GetRenderedLabelName(area);
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var position = AreaUtils.CalculateLabelPosition(EntityManager.GetComponentData<Geometry>(area));
                    entries.Add(new AreaLabelEntry(area, name, position, false));
                    targets.Add((area, position, Layout(name).Lines));
                }
            }
            return entries;
        }
        private void Pick()
        {
            Hovered = Entity.Null;
            var input = Game.Input.InputManager.instance;
            bool defaultActive = tools.activeTool == defaultTool;
            bool overWorld = input != null && input.controlOverWorld;
            if (!defaultActive || !overWorld || rendering.hideOverlay || targets.Count == 0 || !cameras.TryGetViewer(out var viewer)) return;
            var ray = ToolRaycastSystem.CalculateRaycastLine(viewer.camera);
            bool labelHit = AreaLabelPicking.TryPick(ray, viewer.right, targets, out var label, out var labelDistance);
            if (!labelHit) return;
            bool vanillaHit = toolRaycast.GetRaycastResult(out var result) && !EntityManager.HasComponent<Deleted>(result.m_Owner);
            float vanillaDistance = vanillaHit ? result.m_Hit.m_NormalizedDistance : 0f;
            // Hover follows the same priority as the click, so a highlighted label is always the one a click selects.
            Hovered = AreaLabelPicking.ResolveClick(true, overWorld, defaultActive, labelHit, label, labelDistance, vanillaHit, vanillaDistance);
            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool pressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
            var target = AreaLabelPicking.ResolveClick(pressed, overWorld, defaultActive, labelHit, label, labelDistance, vanillaHit, vanillaDistance);
            if (target == Entity.Null) return;
            // Vanilla already played its select sound when it selected something under the label this frame.
            bool vanillaSelected = tools.selected != Entity.Null;
            // Written to ToolSystem directly: SelectedInfoUISystem.SetSelection compares with the panel's previous entity, which
            // may already be this Area while vanilla has just replaced tools.selected with the building under the label.
            tools.selected = target;
            tools.selectedIndex = -1;
            if (!vanillaSelected && !soundSettings.IsEmptyIgnoreFilter)
                audio.PlayUISound(soundSettings.GetSingleton<ToolUXSoundSettingsData>().m_SelectEntitySound);
        }
        // AreaBufferSystem.UpdateLabelVertices: the shared overlay TextMeshPro, one name material per font atlas texture.
        private AreaLabelLayout Layout(string name)
        {
            if (layouts.TryGetValue(name, out var cached)) return cached;
            var text = overlay.GetTextMesh();
            text.rectTransform.sizeDelta = new Vector2(250f, 100f);
            text.fontSize = 200f;
            text.alignment = TextAlignmentOptions.Center;
            var info = text.GetTextInfo(name);
            var vertices = new List<AreaLabelGlyphVertex>();
            for (int i = 0; i < info.meshInfo.Length; i++)
            {
                var meshInfo = info.meshInfo[i];
                if (meshInfo.vertexCount == 0) continue;
                int material = MaterialFor(meshInfo.material);
                for (int n = 0; n < meshInfo.vertexCount; n++)
                    vertices.Add(new AreaLabelGlyphVertex {
                        Position = meshInfo.vertices[n], Color = meshInfo.colors32[n], UV0 = meshInfo.uvs0[n], UV1 = meshInfo.uvs2[n], Material = material
                    });
            }
            var lines = new Bounds2[info.lineCount];
            for (int i = 0; i < info.lineCount; i++)
            {
                var extents = info.lineInfo[i].lineExtents;
                lines[i] = new Bounds2(extents.min, extents.max);
            }
            var layout = new AreaLabelLayout(vertices, lines);
            layouts[name] = layout;
            return layout;
        }
        private int MaterialFor(Material font)
        {
            var texture = font.mainTexture;
            for (int i = 0; i < materials.Count; i++) if (materials[i].mainTexture == texture) return i;
            var material = new Material(nameMaterial);
            overlay.CopyFontAtlasParameters(font, material);
            material.name = $"Managed area names {materials.Count} ({nameMaterial.name})";
            materials.Add(material);
            materialsChanged = true;
            return materials.Count - 1;
        }
        private void Rebuild(List<AreaLabelEntry> entries)
        {
            if (entries.Count == 0 || materials.Count == 0)
            {
                // Nothing to draw: skip the zero-submesh mesh setup entirely.
                if (mesh != null) mesh.Clear();
                drawSubMesh = new bool[0];
                shown = entries;
                materialsChanged = false;
                return;
            }
            var instances = new List<AreaLabelInstance>(entries.Count);
            var bounds = new Bounds3(new float3(float.MaxValue), new float3(float.MinValue));
            foreach (var entry in entries)
            {
                instances.Add(new AreaLabelInstance(entry.Position, entry.Highlighted ? colors.m_SelectedColor : colors.m_Color, Layout(entry.Name)));
                bounds |= EntityManager.GetComponentData<Geometry>(entry.Area).m_Bounds;
            }
            var data = AreaLabelMesh.Build(instances, materials.Count);
            if (mesh == null) mesh = new Mesh { name = "Managed area names" };
            mesh.Clear();
            mesh.SetVertexBufferParams(data.Vertices.Length, AreaLabelVertex.Layout());
            mesh.SetVertexBufferData(data.Vertices, 0, 0, data.Vertices.Length);
            mesh.SetIndexBufferParams(data.Indices.Length, IndexFormat.UInt32);
            mesh.SetIndexBufferData(data.Indices, 0, 0, data.Indices.Length);
            mesh.subMeshCount = data.SubMeshes.Length;
            drawSubMesh = new bool[data.SubMeshes.Length];
            const MeshUpdateFlags flags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds;
            for (int i = 0; i < data.SubMeshes.Length; i++)
            {
                mesh.SetSubMesh(i, data.SubMeshes[i], flags);
                drawSubMesh[i] = data.SubMeshes[i].vertexCount > 0;
            }
            // Labels scale with camera distance in the shader, so the bounds reach well past the Areas themselves.
            if (entries.Count > 0) mesh.bounds = RenderingUtils.ToBounds(MathUtils.Expand(bounds, BoundsMargin));
            // Drop layouts of names no longer shown, so renamed Areas do not grow the cache.
            if (layouts.Count > entries.Count * 2 + 16)
            {
                var keep = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in entries) keep.Add(entry.Name);
                foreach (var key in new List<string>(layouts.Keys)) if (!keep.Contains(key)) layouts.Remove(key);
            }
            shown = entries;
            materialsChanged = false;
        }
        private void Clear()
        {
            if (shown == null && drawSubMesh.Length == 0 && Hovered == Entity.Null) return;
            Hovered = Entity.Null;
            targets.Clear();
            shown = null;
            drawSubMesh = new bool[0];
            if (mesh != null) mesh.Clear();
        }
        private void Render(ScriptableRenderContext context, List<Camera> cameraList)
        {
            if (failed || mesh == null || rendering == null || rendering.hideOverlay) return;
            try
            {
                for (int i = 0; i < drawSubMesh.Length && i < materials.Count; i++)
                {
                    if (!drawSubMesh[i]) continue;
                    foreach (var camera in cameraList)
                        if (camera.cameraType == CameraType.Game || camera.cameraType == CameraType.SceneView)
                            Graphics.DrawMesh(mesh, Matrix4x4.identity, materials[i], 0, camera, i, null, false, false);
                }
            }
            catch (Exception error)
            {
                failed = true;
                Mod.Log.Error(error, "Area label rendering disabled until the next game load");
            }
        }
    }
}
