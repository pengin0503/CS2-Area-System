using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    public enum ParkEntranceEvidenceStatus : byte { Unknown, Complete }
    public readonly struct ParkEntrancePoint
    {
        public readonly float X, Z;
        // Runtime hint only. This type is deliberately not an ECS component or serializable.
        public readonly Entity Edge;
        public ParkEntrancePoint(float x, float z, Entity edge) { X=x; Z=z; Edge=edge; }
    }
    public sealed class ParkEntranceEvidence
    {
        public const int MaxPoints=256;
        public ParkEntranceEvidenceStatus Status { get; }
        public long Revision { get; }
        public IReadOnlyList<ParkEntrancePoint> Points { get; }
        public int Count => Points.Count;
        public ParkEntranceEvidence(ParkEntranceEvidenceStatus status, long revision, IEnumerable<ParkEntrancePoint> points=null)
        {
            Status=status;Revision=revision;
            Points=Array.AsReadOnly(status==ParkEntranceEvidenceStatus.Complete?(points??Array.Empty<ParkEntrancePoint>()).Take(MaxPoints).ToArray():Array.Empty<ParkEntrancePoint>());
        }
    }
    public sealed class ParkEntranceEvidenceStore
    {
        private readonly Dictionary<ulong,ParkEntranceEvidence> entries=new Dictionary<ulong,ParkEntranceEvidence>();
        public long Revision { get; private set; }
        public bool TryGet(ulong id,out ParkEntranceEvidence evidence)=>entries.TryGetValue(id,out evidence);
        public void Set(ulong id,ParkEntranceEvidence evidence) { if(id==0||evidence==null)throw new ArgumentException("Valid Area ID and evidence required");entries[id]=evidence;Revision++; }
        public void Remove(ulong id) { if(entries.Remove(id))Revision++; }
        public void Clear() { entries.Clear();Revision++; }
    }
}
