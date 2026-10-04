using System;
using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using Game;
using Unity.Entities;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Industrial
{
    // Exact-input cache of advisory evaluations per source Area (runtime only). Published observation results are immutable,
    // so their reference identity is an exact observation key; nothing is recomputed while every input is unchanged.
    public sealed class IndustrialSupplyContractCache
    {
        private struct Key
        {
            public IndustrialSupplyContract Contract;
            public IndustrialObservationResult Source, Destination;
            public bool SourceStale, DestinationStale, DestinationLive;
            public bool Matches(in Key o) =>
                Contract.ContractId == o.Contract.ContractId && Contract.DestinationStableId == o.Contract.DestinationStableId && Contract.Resource == o.Contract.Resource &&
                Contract.TargetPerDay == o.Contract.TargetPerDay && Contract.Enabled == o.Contract.Enabled && ReferenceEquals(Source, o.Source) &&
                ReferenceEquals(Destination, o.Destination) && SourceStale == o.SourceStale && DestinationStale == o.DestinationStale && DestinationLive == o.DestinationLive;
        }
        private sealed class Entry { public readonly List<Key> Keys = new List<Key>(); public readonly List<IndustrialSupplyContractEvaluation> Results = new List<IndustrialSupplyContractEvaluation>(); }
        private readonly Dictionary<ulong, Entry> entries = new Dictionary<ulong, Entry>();
        private readonly List<ulong> stale = new List<ulong>();
        // Evaluations actually computed (diagnostics/tests).
        public int Evaluations { get; private set; }
        public int Sources => entries.Count;
        public IReadOnlyList<IndustrialSupplyContractEvaluation> Evaluate(ulong source, IReadOnlyList<IndustrialSupplyContract> contracts, IndustrialSupplySide sourceSide,
            Func<ulong, (bool Live, IndustrialSupplySide Side)> destination)
        {
            if (!entries.TryGetValue(source, out var entry)) entries[source] = entry = new Entry();
            var keys = new List<Key>(contracts.Count); var results = new List<IndustrialSupplyContractEvaluation>(contracts.Count);
            for (int i = 0; i < contracts.Count; i++)
            {
                var c = contracts[i];
                var (live, side) = destination(c.DestinationStableId);
                var key = new Key { Contract = c, Source = sourceSide.Result, SourceStale = sourceSide.Stale, Destination = side.Result, DestinationStale = side.Stale, DestinationLive = live };
                int previous = -1;
                for (int j = 0; j < entry.Keys.Count; j++) if (entry.Keys[j].Contract.ContractId == c.ContractId) { previous = j; break; }
                IndustrialSupplyContractEvaluation result;
                if (previous >= 0 && entry.Keys[previous].Matches(key)) result = entry.Results[previous];
                else { result = IndustrialSupplyContractEvaluator.Evaluate(c, source, live, sourceSide, side); Evaluations++; }
                keys.Add(key); results.Add(result);
            }
            entry.Keys.Clear(); entry.Keys.AddRange(keys); entry.Results.Clear(); entry.Results.AddRange(results);
            return results;
        }
        public void Retain(HashSet<ulong> sources)
        {
            stale.Clear();
            foreach (var id in entries.Keys) if (!sources.Contains(id)) stale.Add(id);
            foreach (var id in stale) entries.Remove(id);
        }
        public void Clear() => entries.Clear();
    }
    // Phase 3 D1 host of runtime-only contract data: per-Area drafts and the evaluation cache. It never touches transfers,
    // routing, prices or demand; per update it only compares the load generation and, every PruneFrames updates, drops
    // drafts/cache entries of removed Industrial Areas.
    [Preserve]
    public partial class IndustrialSupplyContractSystem : GameSystemBase
    {
        public const int PruneFrames = 256;
        private readonly Dictionary<ulong, IndustrialSupplyContractDraft> drafts = new Dictionary<ulong, IndustrialSupplyContractDraft>();
        private readonly List<IndustrialAreaRef> areas = new List<IndustrialAreaRef>();
        private readonly HashSet<ulong> alive = new HashSet<ulong>();
        private readonly List<ulong> removed = new List<ulong>();
        private IndustrialRuntimeSystem runtime;
        private int loadGeneration = -1, sincePrune;
        public IndustrialSupplyContractCache Cache { get; } = new IndustrialSupplyContractCache();
        public IndustrialSupplyContractDraft Draft(ulong area)
        {
            if (!drafts.TryGetValue(area, out var draft)) drafts[area] = draft = new IndustrialSupplyContractDraft();
            return draft;
        }
        // The only writer of an Area's saved contracts: applies one already guarded panel action (the facade checked the
        // Industrial Area, live selection and quarantine) and writes the buffer/allocator back only when they changed.
        internal bool TryApplyAction(Entity area, ulong areaId, string action, string value, Func<ulong, bool> isLiveDestination, Func<ulong, bool> isOfferedResource)
        {
            if (!Enabled || !EntityManager.HasComponent<IndustrialSupplyContractState>(area) || !EntityManager.HasBuffer<IndustrialSupplyContract>(area)) return false;
            var buffer = EntityManager.GetBuffer<IndustrialSupplyContract>(area, true);
            var contracts = new List<IndustrialSupplyContract>(buffer.Length);
            for (int i = 0; i < buffer.Length; i++) contracts.Add(buffer[i]);
            var state = EntityManager.GetComponentData<IndustrialSupplyContractState>(area);
            if (!IndustrialSupplyContractActions.TryApply(action, value, areaId, Draft(areaId), contracts, ref state, isLiveDestination, isOfferedResource, out bool changed)) return false;
            if (!changed) return true;
            var write = EntityManager.GetBuffer<IndustrialSupplyContract>(area);
            write.Clear();
            foreach (var c in contracts) write.Add(c);
            EntityManager.SetComponentData(area, state);
            return true;
        }
        [Preserve] protected override void OnCreate() { base.OnCreate(); runtime = World.GetOrCreateSystemManaged<IndustrialRuntimeSystem>(); }
        [Preserve] protected override void OnUpdate()
        {
            if (runtime.LoadGeneration != loadGeneration) { loadGeneration = runtime.LoadGeneration; drafts.Clear(); Cache.Clear(); sincePrune = 0; return; }
            if (++sincePrune < PruneFrames) return;
            sincePrune = 0;
            runtime.CollectAreas(areas);
            IndustrialAreaIds.Fill(areas, alive);
            Cache.Retain(alive);
            removed.Clear();
            foreach (var id in drafts.Keys) if (!alive.Contains(id)) removed.Add(id);
            foreach (var id in removed) drafts.Remove(id);
        }
    }
}
