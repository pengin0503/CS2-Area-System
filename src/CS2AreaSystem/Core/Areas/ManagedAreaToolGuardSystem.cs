using System.Collections.Generic;
using CS2AreaSystem.Core.Prefabs;
using Game;
using Game.Tools;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Core.Areas
{
    public readonly struct ManagedAreaToolDecision
    {
        public bool Allow { get; }
        public bool NewlyBlocked { get; }
        public bool Recovered { get; }
        public ManagedAreaToolDecision(bool allow, bool newlyBlocked, bool recovered) { Allow = allow; NewlyBlocked = newlyBlocked; Recovered = recovered; }
    }
    // Fail-safe for a managed Area prefab selected with a tool other than the native AreaToolSystem. A mismatch blocks that
    // Area type and closes the tool at once (no retry loop in the same frame); the block is per type, lasts at most until the
    // native tool is observed with that prefab again, and never outlives the load generation it was observed in.
    public sealed class ManagedAreaToolGuard
    {
        private readonly HashSet<int> blocked = new HashSet<int>();
        public bool IsBlocked(int typeId) => blocked.Contains(typeId);
        public ManagedAreaToolDecision Validate(int typeId, bool prefabReady, bool nativeTool)
        {
            if (!nativeTool) return new ManagedAreaToolDecision(false, blocked.Add(typeId), false);
            bool recovered = blocked.Remove(typeId);
            return new ManagedAreaToolDecision(AreaToolActivationPolicy.CanCreate(prefabReady, true), false, recovered);
        }
        public void NewLoadGeneration() => blocked.Clear();
    }
    // PreTool only: validates the tool a managed Area prefab was selected with before the tool runs. It makes no structural
    // change; Area identity, stable IDs and anchor buffers belong to ManagedAreaLifecycleSystem in ModificationEnd.
    public partial class ManagedAreaToolGuardSystem : GameSystemBase
    {
        private AreaPrefabRegistrationSystem registration;
        private ToolSystem tools;
        private readonly ManagedAreaTypeResolver resolver = new ManagedAreaTypeResolver();
        public ManagedAreaToolGuard Guard { get; } = new ManagedAreaToolGuard();
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            registration = World.GetOrCreateSystemManaged<AreaPrefabRegistrationSystem>();
            tools = World.GetOrCreateSystemManaged<ToolSystem>();
        }
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode) => Guard.NewLoadGeneration();
        [Preserve]
        protected override void OnUpdate()
        {
            var prefab = tools.activePrefab;
            if (prefab == null || !resolver.TryResolve(prefab.name, out var kind)) return;
            var decision = Guard.Validate((int)kind, registration.IsTypeReady((int)kind), tools.activeTool is AreaToolSystem);
            if (decision.NewlyBlocked) Mod.Log.Error("Managed area prefab selected an unexpected tool; creation of " + prefab.name + " is blocked until the native area tool is used again");
            if (decision.Recovered) Mod.Log.Info("Native area tool restored for " + prefab.name + "; creation is allowed again");
            if (!decision.Allow) tools.ActivatePrefabTool(null);
        }
    }
}
