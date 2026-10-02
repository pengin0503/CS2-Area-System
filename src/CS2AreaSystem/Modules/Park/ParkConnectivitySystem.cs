using System;
using System.Collections.Generic;
using System.Linq;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Anchors;
using Game;
using Game.Common;
using Game.Tools;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    public partial class ParkConnectivitySystem : GameSystemBase
    {
        public const int EdgesPerUpdate=4096,MaxTargets=4096;
        private ParkRuntimeSystem runtime;private AreaCoreServicesSystem services;private EntityQuery changedNets,changedAreas;
        private readonly List<ParkAreaRef> parks=new List<ParkAreaRef>();
        private readonly Dictionary<ulong,Entry> entries=new Dictionary<ulong,Entry>();
        private int generation=-1,updates;private ActiveScan current;
        private sealed class Entry {public Entity Area;public ulong Members;public long Entrance,Semantics,Anchors;public uint Frame;public readonly ParkConnectivityEpoch Epoch=new ParkConnectivityEpoch();public bool Dirty=true;}
        private sealed class ActiveScan {public ulong Id;public long Revision;public ParkConnectivityScan Graph;}
        public int LastWork { get; private set; }
        protected override void OnCreate()
        {
            base.OnCreate();runtime=World.GetOrCreateSystemManaged<ParkRuntimeSystem>();services=World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            changedNets=GetEntityQuery(new EntityQueryDesc{All=new[]{ComponentType.ReadOnly<Game.Net.Edge>()},Any=new[]{ComponentType.ReadOnly<Created>(),ComponentType.ReadOnly<Updated>(),ComponentType.ReadOnly<Deleted>()},None=new[]{ComponentType.ReadOnly<Temp>()}});
            changedAreas=GetEntityQuery(new EntityQueryDesc{All=new[]{ComponentType.ReadOnly<ManagedArea>()},Any=new[]{ComponentType.ReadOnly<Updated>(),ComponentType.ReadOnly<Created>(),ComponentType.ReadOnly<Deleted>()},None=new[]{ComponentType.ReadOnly<Temp>()}});
        }
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose,GameMode mode){entries.Clear();current=null;generation=-1;}
        protected override void OnUpdate()
        {
            if(!runtime.Ready)return;LastWork=0;
            if(generation!=services.Identity.LoadGeneration){generation=services.Identity.LoadGeneration;entries.Clear();current=null;}
            if(entries.Count==0||!changedAreas.IsEmptyIgnoreFilter||updates++%256==0)Sync();
            bool netChanged=!changedNets.IsEmptyIgnoreFilter;uint frame=runtime.SimulationFrame;
            foreach(var pair in entries) {
                var e=pair.Value;ulong members=runtime.Membership.GetAreaRevision(pair.Key);
                long entrances=runtime.Entrances.TryGet(pair.Key,out var evidence)?evidence.Revision:-1;
                long semantics=services.Semantics.Revision,anchors=unchecked((long)services.Anchors.Revision);
                if(netChanged||!changedAreas.IsEmptyIgnoreFilter||members!=e.Members||entrances!=e.Entrance||semantics!=e.Semantics||anchors!=e.Anchors||!e.Dirty&&current?.Id!=pair.Key&&unchecked(frame-e.Frame)>=ParkEntranceSystem.RefreshFrames) {
                    e.Members=members;e.Entrance=entrances;e.Semantics=semantics;e.Anchors=anchors;e.Epoch.Invalidate();e.Dirty=true;
                    runtime.Store.GetOrCreate(pair.Key,e.Area).Connectivity=e.Epoch.Current;
                }
            }
            if(current!=null&&(!entries.TryGetValue(current.Id,out var active)||current.Revision!=active.Epoch.Revision))current=null;
            if(current==null) {
                var next=entries.Where(p=>p.Value.Dirty).OrderBy(p=>p.Key).FirstOrDefault();if(next.Value==null)return;
                try{current=Begin(next.Key,next.Value);}catch(Exception error){next.Value.Dirty=false;next.Value.Frame=frame;services.Failures.Report(next.Value.Area,"Park.Connectivity",error,"Park connectivity scan unavailable");return;}
            }
            var scan=current;
            try {
                scan.Graph.Step(EdgesPerUpdate);LastWork=scan.Graph.LastWork;
                if(!entries.TryGetValue(scan.Id,out var entry))return;
                var result=scan.Graph.Snapshot();result.Frame=frame;result.MembershipRevision=entry.Members;result.EntranceRevision=entry.Entrance;result.SemanticRevision=entry.Semantics;
                if(entry.Epoch.TryPublish(scan.Revision,result))runtime.Store.GetOrCreate(scan.Id,entry.Area).Connectivity=result;
                if(scan.Graph.Done){entry.Dirty=false;entry.Frame=frame;current=null;services.Failures.Recovered(entry.Area,"Park.Connectivity");}
            }catch(Exception error){current=null;if(entries.TryGetValue(scan.Id,out var entry)){entry.Dirty=false;entry.Frame=frame;entry.Epoch.Invalidate();runtime.Store.GetOrCreate(scan.Id,entry.Area).Connectivity=entry.Epoch.Current;services.Failures.Report(entry.Area,"Park.Connectivity",error,"Park connectivity scan failed; no disconnected result assumed");}}
        }
        private void Sync()
        {
            runtime.CollectParks(parks);var alive=new HashSet<ulong>();
            foreach(var park in parks){alive.Add(park.StableId);if(!entries.TryGetValue(park.StableId,out var e))entries[park.StableId]=new Entry{Area=park.Area};else if(e.Area!=park.Area){e.Area=park.Area;e.Epoch.Invalidate();e.Dirty=true;}}
            foreach(var id in entries.Keys.Where(id=>!alive.Contains(id)).ToArray())entries.Remove(id);
        }
        private ActiveScan Begin(ulong id,Entry entry)
        {
            var seeds=new List<Entity>();
            if(runtime.Entrances.TryGet(id,out var evidence)&&evidence.Status==ParkEntranceEvidenceStatus.Complete)seeds.AddRange(evidence.Points.Select(p=>p.Edge));
            foreach(var entrance in services.Anchors.GetValidAnchors(entry.Area,AreaAnchorRole.Entrance).Take(ParkConnectivityScan.MaxEdges))seeds.Add(Road(entrance));
            var targets=new List<(Entity,Entity)>();int visited=0;
            foreach(var member in runtime.Membership.GetMembers(id).Take(MaxTargets+1)) {
                if(++visited>MaxTargets)break;
                if(services.IsValidEntity(member)&&ParkFacilityClassifier.IsVisitRelevant(ParkFacilityReader.ReadCore(member,EntityManager,false),services.Semantics))targets.Add((member,Road(member)));
            }
            var graph=new ParkConnectivityScan(seeds,targets,ReadEdge,ReadNeighbor);
            if(visited>MaxTargets)graph.MarkPartial();
            return new ActiveScan{Id=id,Revision=entry.Epoch.Revision,Graph=graph};
        }
        private Entity Road(Entity building)=>services.IsValidEntity(building)&&EntityManager.HasComponent<Game.Buildings.Building>(building)?EntityManager.GetComponentData<Game.Buildings.Building>(building).m_RoadEdge:Entity.Null;
        private ParkConnectivityEdge ReadEdge(Entity edge)
        {
            if(!services.IsValidEntity(edge)||!EntityManager.HasComponent<Game.Net.Edge>(edge)||!EntityManager.HasBuffer<Game.Net.SubLane>(edge))return default;
            var value=EntityManager.GetComponentData<Game.Net.Edge>(edge);var lanes=EntityManager.GetBuffer<Game.Net.SubLane>(edge,true);
            bool pedestrian=false;int count=Math.Min(lanes.Length,256);
            for(int i=0;i<count;i++){var lane=lanes[i].m_SubLane;if(services.IsValidEntity(lane)&&EntityManager.HasComponent<Game.Net.PedestrianLane>(lane)){pedestrian=true;break;}}
            bool known=pedestrian||lanes.Length<=256;
            known&=services.IsValidEntity(value.m_Start)&&services.IsValidEntity(value.m_End)&&EntityManager.HasBuffer<Game.Net.ConnectedEdge>(value.m_Start)&&EntityManager.HasBuffer<Game.Net.ConnectedEdge>(value.m_End);
            return new ParkConnectivityEdge(known,pedestrian,value.m_Start,value.m_End);
        }
        private Entity? ReadNeighbor(Entity node,int index)
        {
            if(!services.IsValidEntity(node)||!EntityManager.HasBuffer<Game.Net.ConnectedEdge>(node))return null;
            var edges=EntityManager.GetBuffer<Game.Net.ConnectedEdge>(node,true);return index<edges.Length?(Entity?)edges[index].m_Edge:null;
        }
    }
}
