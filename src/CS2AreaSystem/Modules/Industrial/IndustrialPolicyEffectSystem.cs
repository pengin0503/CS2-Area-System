using System.Collections.Generic;
using CS2AreaSystem.Core.Policies;
using CS2AreaSystem.Modules.Industrial.Policies;
using Game;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Industrial
{
    // Keeps each Industrial Area's vanilla policy effects in step with its effective policy state (design §9.2, §12).
    // Adapters run only on transitions; a load starts from a clean controller and re-applies effective policies.
    // With no verified adapter (1.6.2f1) the system has nothing to do and returns at once.
    public partial class IndustrialPolicyEffectSystem : GameSystemBase
    {
        public const int UpdateFrames = 256;
        private IndustrialRuntimeSystem runtime;
        private AreaPolicySystem policies;
        private readonly List<IndustrialAreaRef> areas = new List<IndustrialAreaRef>();
        private readonly Dictionary<ulong, Unity.Entities.Entity> synced = new Dictionary<ulong, Unity.Entities.Entity>();
        private readonly HashSet<ulong> alive = new HashSet<ulong>();
        private bool failureLogged;
        public IndustrialPolicyEffectController Controller { get; private set; }
        public override int GetUpdateInterval(SystemUpdatePhase phase) => UpdateFrames;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            runtime = World.GetOrCreateSystemManaged<IndustrialRuntimeSystem>();
            policies = World.GetOrCreateSystemManaged<AreaPolicySystem>();
            Controller = new IndustrialPolicyEffectController(IndustrialPolicyAdapters.Create(),
                (error, key) => Mod.Log.Error(error, "Industrial policy effect disabled for this load (" + key + ")"));
        }
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            Controller.Reset(); synced.Clear();
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!Controller.HasAdapters || !runtime.Ready) return;
            try
            {
                var context = new AreaPolicyContext(World, EntityManager);
                runtime.CollectAreas(areas);
                alive.Clear();
                foreach (var area in areas)
                {
                    alive.Add(area.StableId); synced[area.StableId] = area.Area;
                    foreach (int policyId in Controller.PolicyIds)
                        Controller.Sync(area.Area, area.StableId, policyId, policies.IsEffective(area.Area, policyId), context);
                }
                var gone = new List<ulong>();
                foreach (var entry in synced) if (!alive.Contains(entry.Key)) gone.Add(entry.Key);
                foreach (var id in gone) { Controller.Release(synced[id], id, context); synced.Remove(id); }
                failureLogged = false;
            }
            catch (System.Exception error)
            {
                // Adapter failures are isolated by the controller; this guards the policy-state reads.
                if (!failureLogged) { failureLogged = true; Mod.Log.Error(error, "Industrial policy effect update failed; effects keep their last state"); }
            }
        }
    }
}
