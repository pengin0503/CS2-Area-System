using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Mathematics;
using Unity.Entities;
using Unity.Mathematics;
namespace CS2AreaSystem.Modules.Park
{
    public static class ParkPathEntranceDetector
    {
        public const int Segments=16;
        public const float MergeRadius=8f;
        public static IReadOnlyList<ParkEntrancePoint> Detect(Entity edge, bool dedicated, Bezier4x3 curve, Func<float2,bool> contains)
        {
            var result=new List<ParkEntrancePoint>();if(!dedicated)return result;
            bool previous=contains(MathUtils.Position(curve,0).xz);
            for(int i=1;i<=Segments;i++) {
                float end=(float)i/Segments;bool inside=contains(MathUtils.Position(curve,end).xz);
                if(inside!=previous) {
                    float lo=(float)(i-1)/Segments,hi=end;
                    // Refine only detected transitions; the topology contract remains 17 sampled points.
                    for(int step=0;step<10;step++){float mid=(lo+hi)*0.5f;if(contains(MathUtils.Position(curve,mid).xz)==previous)lo=mid;else hi=mid;}
                    var p=MathUtils.Position(curve,(lo+hi)*0.5f);result.Add(new ParkEntrancePoint(p.x,p.z,edge));
                }
                previous=inside;
            }
            return result;
        }
        public static IReadOnlyList<ParkEntrancePoint> Merge(IEnumerable<ParkEntrancePoint> candidates)
        {
            var result=new List<ParkEntrancePoint>();
            foreach(var p in candidates.OrderBy(p=>p.Edge.Index).ThenBy(p=>p.Edge.Version).ThenBy(p=>p.X).ThenBy(p=>p.Z)) {
                if(result.Any(r=>(r.X-p.X)*(r.X-p.X)+(r.Z-p.Z)*(r.Z-p.Z)<=MergeRadius*MergeRadius))continue;
                result.Add(p);if(result.Count==ParkEntranceEvidence.MaxPoints)break;
            }
            return result;
        }
    }
    public readonly struct ParkNetPage
    {
        public readonly IReadOnlyList<Entity> Edges;public readonly bool Overflow;
        public ParkNetPage(IReadOnlyList<Entity> edges,bool overflow){Edges=edges;Overflow=overflow;}
    }
    // The public vanilla quadtree cannot suspend an iterator. Overflowing queries are discarded and spatially
    // subdivided; each update has a single shared enumeration/classification budget. No partial result activates.
    public sealed class ParkEntranceScan
    {
        public ulong StableId { get; } public long Revision { get; }
        public int LastWork { get; private set; }
        public bool Done=>tiles.Count==0&&cursor>=pending.Count;
        public bool Complete { get; private set; }=true;
        private readonly Queue<(Bounds2 Bounds,int Depth)> tiles=new Queue<(Bounds2,int)>();
        private readonly HashSet<Entity> seen=new HashSet<Entity>();
        private IReadOnlyList<Entity> pending=Array.Empty<Entity>();private int cursor,totalQueries;
        private readonly List<ParkEntrancePoint> points=new List<ParkEntrancePoint>();
        public IReadOnlyList<ParkEntrancePoint> Points=>ParkPathEntranceDetector.Merge(points);
        public const int MaxCandidates=32768;
        public const int MaxTileQueries=4096,MaxPendingTiles=4096;
        public ParkEntranceScan(ulong id,long revision,Bounds2 bounds){StableId=id;Revision=revision;tiles.Enqueue((bounds,0));}
        public void Step(Func<Bounds2,int,ParkNetPage> read,Func<Entity,IReadOnlyList<ParkEntrancePoint>> detect,int budget)
        {
            LastWork=0;int queries=0;
            while(LastWork<budget) {
                if(cursor<pending.Count) {
                    var edge=pending[cursor++];LastWork++;
                    if(!seen.Add(edge))continue;
                    if(seen.Count>MaxCandidates){Complete=false;tiles.Clear();pending=Array.Empty<Entity>();cursor=0;break;}
                    var found=detect(edge);
                    if(points.Count+found.Count>MaxCandidates){Complete=false;tiles.Clear();pending=Array.Empty<Entity>();cursor=0;break;}
                    points.AddRange(found);continue;
                }
                if(tiles.Count==0||queries>=64||budget-LastWork<2)break;
                if(totalQueries>=MaxTileQueries){Complete=false;tiles.Clear();break;}
                var tile=tiles.Dequeue();int limit=Math.Max(1,(budget-LastWork)/2);
                var page=read(tile.Bounds,limit);queries++;totalQueries++;
                if(page.Edges.Count>limit)throw new InvalidOperationException("Net enumeration exceeded its budget");
                LastWork+=Math.Max(1,page.Edges.Count);
                if(page.Overflow) {
                    if(tile.Depth>=16||tiles.Count+4>MaxPendingTiles){Complete=false;tiles.Clear();break;}
                    var mid=(tile.Bounds.min+tile.Bounds.max)*0.5f;
                    tiles.Enqueue((new Bounds2(tile.Bounds.min,mid),tile.Depth+1));
                    tiles.Enqueue((new Bounds2(new float2(mid.x,tile.Bounds.min.y),new float2(tile.Bounds.max.x,mid.y)),tile.Depth+1));
                    tiles.Enqueue((new Bounds2(new float2(tile.Bounds.min.x,mid.y),new float2(mid.x,tile.Bounds.max.y)),tile.Depth+1));
                    tiles.Enqueue((new Bounds2(mid,tile.Bounds.max),tile.Depth+1));
                } else {pending=page.Edges;cursor=0;}
            }
        }
    }
    public sealed class ParkEntranceScanCoordinator
    {
        private readonly ParkEntranceEvidenceStore store;
        private readonly Dictionary<ulong,long> revisions=new Dictionary<ulong,long>();private long revision;
        public ParkEntranceScanCoordinator(ParkEntranceEvidenceStore store){this.store=store;}
        public void Track(ulong id){if(!revisions.ContainsKey(id))BoundaryChanged(id);}
        public void BoundaryChanged(ulong id){revisions[id]=++revision;store.Set(id,new ParkEntranceEvidence(ParkEntranceEvidenceStatus.Unknown,revision));}
        public void NetChanged(ulong id){revisions[id]=++revision;}
        public ParkEntranceScan Begin(ulong id,Bounds2 bounds){Track(id);return new ParkEntranceScan(id,revisions[id],bounds);}
        public bool IsCurrent(ParkEntranceScan scan)=>revisions.TryGetValue(scan.StableId,out var rev)&&rev==scan.Revision;
        public bool TryPublish(ParkEntranceScan scan){if(!scan.Done||!scan.Complete||!IsCurrent(scan))return false;store.Set(scan.StableId,new ParkEntranceEvidence(ParkEntranceEvidenceStatus.Complete,scan.Revision,scan.Points));return true;}
        public void Remove(ulong id){revisions.Remove(id);store.Remove(id);}
        public void Clear(){revisions.Clear();store.Clear();revision++;}
    }
}
