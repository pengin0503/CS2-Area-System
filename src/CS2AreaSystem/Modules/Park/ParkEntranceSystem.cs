using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Collections;
using Colossal.Mathematics;
using CS2AreaSystem.Core.Areas;
using Game;
using Game.Areas;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace CS2AreaSystem.Modules.Park
{
    public partial class ParkEntranceSystem : GameSystemBase
    {
        public const int EdgesPerUpdate=2048,ChangedNetCutoff=512;
        public const float Margin=16f;
        public const uint RefreshFrames=ParkRuntimeSystem.FramesPerDay/8;
        private ParkRuntimeSystem runtime;private AreaCoreServicesSystem services;private Game.Net.SearchSystem search;
        private EntityQuery changedParks,changedNets;private ParkEntranceScanCoordinator coordinator;
        private readonly List<ParkAreaRef> parks=new List<ParkAreaRef>();
        private readonly Dictionary<ulong,(Entity Area,Bounds2 Box,uint Frame)> tracked=new Dictionary<ulong,(Entity,Bounds2,uint)>();
        private readonly HashSet<ulong> dirty=new HashSet<ulong>();
        private ParkEntranceScan scan;private ParkPolygon polygon;private int generation=-1,updates;
        public int LastWork { get; private set; }
        public int MaxWork { get; private set; }
        protected override void OnCreate()
        {
            base.OnCreate();runtime=World.GetOrCreateSystemManaged<ParkRuntimeSystem>();services=World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            search=World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();coordinator=new ParkEntranceScanCoordinator(runtime.Entrances);
            changedParks=GetEntityQuery(new EntityQueryDesc{All=new[]{ComponentType.ReadOnly<ManagedArea>()},Any=new[]{ComponentType.ReadOnly<Created>(),ComponentType.ReadOnly<Updated>(),ComponentType.ReadOnly<Deleted>()},None=new[]{ComponentType.ReadOnly<Temp>()}});
            changedNets=GetEntityQuery(new EntityQueryDesc{All=new[]{ComponentType.ReadOnly<Game.Net.Edge>(),ComponentType.ReadOnly<Game.Net.Curve>()},Any=new[]{ComponentType.ReadOnly<Created>(),ComponentType.ReadOnly<Updated>(),ComponentType.ReadOnly<Deleted>()},None=new[]{ComponentType.ReadOnly<Temp>()}});
        }
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose,GameMode mode){coordinator.Clear();tracked.Clear();dirty.Clear();scan=null;generation=-1;}
        protected override void OnUpdate()
        {
            if(!runtime.Ready)return;LastWork=0;
            if(generation!=services.Identity.LoadGeneration){generation=services.Identity.LoadGeneration;coordinator.Clear();tracked.Clear();dirty.Clear();scan=null;}
            if(tracked.Count==0||!changedParks.IsEmptyIgnoreFilter||updates++%256==0)Sync();
            uint frame=runtime.SimulationFrame;
            foreach(var p in tracked.ToArray())if(unchecked(frame-p.Value.Frame)>=RefreshFrames&&!dirty.Contains(p.Key)&&scan?.StableId!=p.Key)MarkNet(p.Key);
            MarkNetChanges();
            if(scan!=null&&!coordinator.IsCurrent(scan))scan=null;
            if(scan==null) {
                if(dirty.Count==0)return;ulong id=dirty.Min();dirty.Remove(id);
                if(!tracked.TryGetValue(id,out var area)||!services.IsValidEntity(area.Area))return;
                polygon=new ParkPolygon(ParkGeometry.Triangles(EntityManager,area.Area));scan=coordinator.Begin(id,area.Box);
            }
            var current=scan;
            try {
                current.Step(ReadPage,Detect,EdgesPerUpdate);LastWork=current.LastWork;MaxWork=Math.Max(MaxWork,LastWork);
                if(!current.Done)return;
                scan=null;
                if(!tracked.TryGetValue(current.StableId,out var area))return;
                bool oldKnown=runtime.Entrances.TryGet(current.StableId,out var old);
                if(coordinator.TryPublish(current)) {
                    runtime.Entrances.TryGet(current.StableId,out var evidence);
                    tracked[current.StableId]=(area.Area,area.Box,frame);
                    runtime.Store.GetOrCreate(current.StableId,area.Area).EntranceEvidence=evidence;
                    // Revision changes also refresh connectivity/location UI when count stays the same.
                    if(!oldKnown||old.Revision!=evidence.Revision||old.Count!=evidence.Count||old.Status!=evidence.Status)services.Anchors.InvalidateArea(area.Area);
                } else tracked[current.StableId]=(area.Area,area.Box,frame);
                services.Failures.Recovered(area.Area,"Park.Entrance");
            } catch(Exception error) {scan=null;if(tracked.TryGetValue(current.StableId,out var area)){tracked[current.StableId]=(area.Area,area.Box,frame);services.Failures.Report(area.Area,"Park.Entrance",error,"Park entrance scan failed; last compatible evidence kept");}}
        }
        private void Sync()
        {
            var edited=new HashSet<Entity>();using(var changes=changedParks.ToEntityArray(Allocator.Temp))foreach(var e in changes)edited.Add(e);
            runtime.CollectParks(parks);var alive=new HashSet<ulong>();
            foreach(var park in parks) {
                alive.Add(park.StableId);if(!EntityManager.HasComponent<Geometry>(park.Area))continue;
                var native=EntityManager.GetComponentData<Geometry>(park.Area).m_Bounds.xz;var box=new Bounds2(native.min-Margin,native.max+Margin);
                if(!tracked.TryGetValue(park.StableId,out var old)||old.Area!=park.Area||!old.Box.Equals(box)||edited.Contains(park.Area)) {
                    tracked[park.StableId]=(park.Area,box,runtime.SimulationFrame);coordinator.BoundaryChanged(park.StableId);dirty.Add(park.StableId);
                    runtime.Store.GetOrCreate(park.StableId,park.Area).EntranceEvidence=new ParkEntranceEvidence(ParkEntranceEvidenceStatus.Unknown,0);
                    services.Anchors.InvalidateArea(park.Area);
                }
            }
            foreach(var id in tracked.Keys.Where(id=>!alive.Contains(id)).ToArray()){tracked.Remove(id);dirty.Remove(id);coordinator.Remove(id);}
        }
        private void MarkNet(ulong id){coordinator.NetChanged(id);dirty.Add(id);}
        private void MarkNetChanges()
        {
            int count=changedNets.CalculateEntityCount();if(count==0)return;
            if(count>ChangedNetCutoff){foreach(var id in tracked.Keys)MarkNet(id);return;}
            using var changed=changedNets.ToEntityArray(Allocator.Temp);
            foreach(var e in changed) {
                var b=EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier;
                var min=math.min(math.min(b.a.xz,b.b.xz),math.min(b.c.xz,b.d.xz));var max=math.max(math.max(b.a.xz,b.b.xz),math.max(b.c.xz,b.d.xz));
                foreach(var p in tracked)if(math.all(min<=p.Value.Box.max)&&math.all(max>=p.Value.Box.min))MarkNet(p.Key);
            }
        }
        private ParkNetPage ReadPage(Bounds2 box,int limit)
        {
            var tree=search.GetNetSearchTree(true,out var dependency);dependency.Complete();
            var iterator=new PageIterator{Box=box,Limit=limit,Items=new List<Entity>()};tree.Iterate(ref iterator);
            return new ParkNetPage(iterator.Items,iterator.Items.Count>=limit);
        }
        private IReadOnlyList<ParkEntrancePoint> Detect(Entity edge)
        {
            if(!services.IsValidEntity(edge)||!EntityManager.HasComponent<Game.Net.Edge>(edge)||!EntityManager.HasComponent<Game.Net.Curve>(edge)||!EntityManager.HasComponent<PrefabRef>(edge))return Array.Empty<ParkEntrancePoint>();
            var prefab=EntityManager.GetComponentData<PrefabRef>(edge).m_Prefab;
            return ParkPathEntranceDetector.Detect(edge,EntityManager.Exists(prefab)&&EntityManager.HasComponent<PathwayData>(prefab),EntityManager.GetComponentData<Game.Net.Curve>(edge).m_Bezier,polygon.Contains);
        }
        private struct PageIterator : INativeQuadTreeIterator<Entity,QuadTreeBoundsXZ>
        {
            public Bounds2 Box;public int Limit;public List<Entity> Items;
            public bool Intersect(QuadTreeBoundsXZ bounds)=>Items.Count<Limit&&math.all(Box.min<=bounds.m_Bounds.xz.max)&&math.all(Box.max>=bounds.m_Bounds.xz.min);
            public void Iterate(QuadTreeBoundsXZ bounds,Entity edge){if(Intersect(bounds))Items.Add(edge);}
        }
    }
}
