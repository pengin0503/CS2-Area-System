using System;
using System.Collections.Generic;
using System.Linq;
using CS2AreaSystem.Core.Modules;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    public enum ParkConnectivityStatus : byte { Unknown,Partial,Complete }
    public readonly struct ParkConnectivityEdge
    {
        public readonly bool Known,Pedestrian;public readonly Entity Start,End;
        public ParkConnectivityEdge(bool known,bool pedestrian,Entity start,Entity end){Known=known;Pedestrian=pedestrian;Start=start;End=end;}
    }
    public sealed class ParkConnectivitySnapshot
    {
        public ParkConnectivityStatus Status;
        public int Connected,Total,UnknownTargets,Traversed;
        public uint Frame;public ulong MembershipRevision;public long AnchorRevision,SemanticRevision;
        public IReadOnlyList<Entity> DisconnectedTargets=Array.Empty<Entity>();
    }
    // Read-only BFS over the public node/edge graph. Both edge reads and neighbor reads consume the budget;
    // high-degree nodes resume at their next buffer index on the following update.
    public sealed class ParkConnectivityScan
    {
        public const int MaxEdges=20000;
        private readonly Func<Entity,ParkConnectivityEdge> read;
        private readonly Func<Entity,int,Entity?> neighbor;
        private readonly (Entity Facility,Entity Edge)[] targets;
        private readonly Queue<Entity> queue=new Queue<Entity>();
        private readonly HashSet<Entity> queued=new HashSet<Entity>(),reached=new HashSet<Entity>();
        private readonly int cap;private readonly bool seedsKnown;
        private bool limited,missing;private int traversed,nodeSide,index;private ParkConnectivityEdge? current;
        public bool Done { get; private set; }
        public int LastWork { get; private set; }
        public ParkConnectivityScan(IEnumerable<Entity> seeds,IEnumerable<(Entity Facility,Entity Edge)> targets,Func<Entity,ParkConnectivityEdge> read,Func<Entity,int,Entity?> neighbor,int cap=MaxEdges)
        {
            this.read=read;this.neighbor=neighbor;this.targets=targets.ToArray();this.cap=Math.Max(1,cap);
            foreach(var seed in seeds){if(seed==Entity.Null)continue;if(queued.Count>=this.cap){limited=true;break;}if(queued.Add(seed))queue.Enqueue(seed);}
            seedsKnown=queue.Count>0;Done=!seedsKnown;
        }
        public void MarkPartial(){missing=true;}
        public void Step(int budget)
        {
            LastWork=0;
            while(!Done&&LastWork<budget) {
                if(current.HasValue) {
                    var node=nodeSide==0?current.Value.Start:current.Value.End;
                    var next=neighbor(node,index++);LastWork++;
                    if(!next.HasValue){if(nodeSide++==0){index=0;continue;}current=null;continue;}
                    if(next.Value==Entity.Null||queued.Contains(next.Value))continue;
                    if(queued.Count>=cap){limited=true;Done=true;break;}
                    queued.Add(next.Value);queue.Enqueue(next.Value);continue;
                }
                if(queue.Count==0){Done=true;break;}
                var entity=queue.Dequeue();var edge=read(entity);LastWork++;traversed++;
                if(!edge.Known)missing=true;
                else if(edge.Pedestrian){reached.Add(entity);current=edge;nodeSide=index=0;}
                if(traversed>=cap){limited=true;Done=true;}
            }
            if(queue.Count==0&&!current.HasValue)Done=true;
        }
        public ParkConnectivitySnapshot Snapshot()
        {
            var status=!seedsKnown?ParkConnectivityStatus.Unknown:!Done||limited||missing?ParkConnectivityStatus.Partial:ParkConnectivityStatus.Complete;
            return new ParkConnectivitySnapshot{Status=status,Total=targets.Length,Connected=targets.Count(t=>reached.Contains(t.Edge)),UnknownTargets=targets.Count(t=>t.Edge==Entity.Null),Traversed=traversed,
                DisconnectedTargets=status==ParkConnectivityStatus.Complete?targets.Where(t=>t.Edge!=Entity.Null&&!reached.Contains(t.Edge)).Select(t=>t.Facility).OrderBy(e=>e.Index).ThenBy(e=>e.Version).ToArray():Array.Empty<Entity>()};
        }
    }
    public sealed class ParkConnectivityEpoch
    {
        public long Revision { get; private set; }
        public ParkConnectivitySnapshot Current { get; private set; }=new ParkConnectivitySnapshot();
        public void Invalidate(){Revision++;Current=new ParkConnectivitySnapshot();}
        public bool TryPublish(long revision,ParkConnectivitySnapshot result){if(revision!=Revision)return false;Current=result;return true;}
    }
    public static class ParkConnectivityFocus
    {
        public static bool TryFocus(ParkConnectivitySnapshot snapshot,string key,Func<Entity,bool> current,Action<Entity> focus)
        {
            if(snapshot?.Status!=ParkConnectivityStatus.Complete||current==null||focus==null||!AreaDiagnosticTargetKey.TryResolve(key,snapshot.DisconnectedTargets,out var target)||!current(target))return false;
            focus(target);return true;
        }
    }
}
