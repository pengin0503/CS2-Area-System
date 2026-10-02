using System;
using System.Collections.Generic;
using System.Linq;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Diagnostics;
using CS2AreaSystem.Core.Persistence;
using Game;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    public partial class IndustrialHistorySystem : GameSystemBase
    {
        public const int Capacity=168,AreasPerUpdate=4;
        private IndustrialRuntimeSystem runtime;private AreaCoreServicesSystem services;
        private readonly List<IndustrialAreaRef> areas=new List<IndustrialAreaRef>();private int cursor;
        public override int GetUpdateInterval(SystemUpdatePhase phase)=>256;
        protected override void OnCreate(){base.OnCreate();runtime=World.GetOrCreateSystemManaged<IndustrialRuntimeSystem>();services=World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();}
        protected override void OnUpdate(){if(!runtime.Ready)return;runtime.CollectAreas(areas);for(int i=0;i<Math.Min(AreasPerUpdate,areas.Count);i++)Persist(areas[cursor++%areas.Count]);}
        // Save flush is bounded by managed Areas and 168 elements per Area, never by city entities.
        public void Flush(){if(!runtime.Ready)return;runtime.CollectAreas(areas);foreach(var area in areas)Persist(area);}
        private void Persist(IndustrialAreaRef area)
        {
            try {
                var snapshot=runtime.Store.GetOrCreate(area.StableId,area.Area);
                if(!EntityManager.HasBuffer<IndustrialHistoryEntry>(area.Area))EntityManager.AddBuffer<IndustrialHistoryEntry>(area.Area);
                var buffer=EntityManager.GetBuffer<IndustrialHistoryEntry>(area.Area);
                if(buffer.Length>Capacity)throw new System.IO.InvalidDataException("Oversized Area history buffer; not overwritten");
                var loaded=new List<IndustrialHistoryEntry>(buffer.Length);
                for(int i=0;i<buffer.Length;i++){var row=buffer[i];ComponentSchema.RequireSupported(row.SchemaVersion);loaded.Add(row);}
                var rows=Normalize(loaded);bool changed=!loaded.SequenceEqual(rows);
                foreach(var row in Capture(snapshot))changed|=Record(rows,row);
                if(changed){buffer.Clear();foreach(var row in rows)buffer.Add(row);}
                services.Failures.Recovered(area.Area,"Industrial.History");
            }catch(Exception error){services.Failures.Report(area.Area,"Industrial.History",error,"Area history unavailable; gameplay continues without overwriting unsupported history");}
        }
        private static IndustrialHistoryEntry Merge(IndustrialHistoryEntry previous,IndustrialHistoryEntry next)
        {
            previous.SchemaVersion=1;previous.HourBucket=next.HourBucket;
            if((next.Availability&1)!=0)previous.Production=next.Production;
            if((next.Availability&2)!=0)previous.Logistics=next.Logistics;
            if((next.Availability&4)!=0)previous.Employment=next.Employment;
            if((next.Availability&8)!=0)previous.Efficiency=next.Efficiency;
            if((next.Availability&16)!=0)previous.Performance=next.Performance;
            if((next.Availability&32)!=0)previous.ProductiveFacilities=next.ProductiveFacilities;
            previous.Availability|=next.Availability;return previous;
        }
        private static List<IndustrialHistoryEntry> Normalize(IEnumerable<IndustrialHistoryEntry> history) =>
            AreaHourlyHistory.Normalize(history,row=>row.HourBucket,Merge,
                row=>ComponentSchema.RequireSupported(row.SchemaVersion==0?IndustrialHistoryEntry.CurrentSchemaVersion:row.SchemaVersion),Capacity);
        public static bool Record(IList<IndustrialHistoryEntry> rows,IndustrialHistoryEntry next)
        {
            ComponentSchema.RequireSupported(next.SchemaVersion==0?IndustrialHistoryEntry.CurrentSchemaVersion:next.SchemaVersion);
            var normalized=Normalize(rows);bool changed=!rows.SequenceEqual(normalized);
            int index=normalized.FindIndex(row=>row.HourBucket==next.HourBucket);
            if(index>=0){var merged=Merge(normalized[index],next);changed|=!normalized[index].Equals(merged);normalized[index]=merged;}
            else if(normalized.Count==0||next.HourBucket>=normalized[normalized.Count-1].HourBucket){normalized.Add(Merge(default,next));changed=true;}
            while(normalized.Count>Capacity)normalized.RemoveAt(0);
            if(changed){rows.Clear();foreach(var row in normalized)rows.Add(row);}
            return changed;
        }
        public static List<IndustrialHistoryEntry> Capture(IndustrialRuntimeSnapshot snapshot)
        {
            var rows=new SortedDictionary<long,IndustrialHistoryEntry>();var samples=new List<RuntimeTrendSample>(24);
            snapshot.Trends.Production.CopyTo(samples);
            foreach(var sample in samples) { rows.TryGetValue(sample.Bucket,out var row);row.SchemaVersion=1;row.HourBucket=sample.Bucket;row.Availability|=1;row.Production=sample.Value;rows[sample.Bucket]=row; }
            snapshot.Trends.Logistics.CopyTo(samples);
            foreach(var sample in samples) { rows.TryGetValue(sample.Bucket,out var row);row.SchemaVersion=1;row.HourBucket=sample.Bucket;row.Availability|=2;row.Logistics=sample.Value;rows[sample.Bucket]=row; }
            snapshot.Trends.Employment.CopyTo(samples);
            foreach(var sample in samples) { rows.TryGetValue(sample.Bucket,out var row);row.SchemaVersion=1;row.HourBucket=sample.Bucket;row.Availability|=4;row.Employment=sample.Value;rows[sample.Bucket]=row; }
            snapshot.Trends.Efficiency.CopyTo(samples);
            foreach(var sample in samples) { rows.TryGetValue(sample.Bucket,out var row);row.SchemaVersion=1;row.HourBucket=sample.Bucket;row.Availability|=8;row.Efficiency=sample.Value;rows[sample.Bucket]=row; }
            snapshot.Trends.Performance.CopyTo(samples);
            foreach(var sample in samples) { rows.TryGetValue(sample.Bucket,out var row);row.SchemaVersion=1;row.HourBucket=sample.Bucket;row.Availability|=16;row.Performance=sample.Value;rows[sample.Bucket]=row; }
            snapshot.Trends.ProductiveFacilities.CopyTo(samples);
            foreach(var sample in samples) { rows.TryGetValue(sample.Bucket,out var row);row.SchemaVersion=1;row.HourBucket=sample.Bucket;row.Availability|=32;row.ProductiveFacilities=sample.Value;rows[sample.Bucket]=row; }
            return rows.Values.ToList();
        }
        public static void Seed(EntityManager entities,IndustrialRuntimeSnapshot snapshot)
        {
            if(!entities.HasBuffer<IndustrialHistoryEntry>(snapshot.Area))return;
            var buffer=entities.GetBuffer<IndustrialHistoryEntry>(snapshot.Area,true);
            if(buffer.Length>Capacity)throw new System.IO.InvalidDataException("Oversized Area history buffer");
            var rows=new List<IndustrialHistoryEntry>(buffer.Length);
            for(int i=0;i<buffer.Length;i++)rows.Add(buffer[i]);
            Seed(rows,snapshot);
        }
        public static void Seed(IEnumerable<IndustrialHistoryEntry> history,IndustrialRuntimeSnapshot snapshot)
        {
            var normalized=Normalize(history);
            foreach(var row in normalized.Skip(Math.Max(0,normalized.Count-24))) {
                if((row.Availability&1)!=0)snapshot.Trends.Production.Record(row.HourBucket,row.Production);
                if((row.Availability&2)!=0)snapshot.Trends.Logistics.Record(row.HourBucket,row.Logistics);
                if((row.Availability&4)!=0)snapshot.Trends.Employment.Record(row.HourBucket,row.Employment);
                if((row.Availability&8)!=0)snapshot.Trends.Efficiency.Record(row.HourBucket,row.Efficiency);
                if((row.Availability&16)!=0)snapshot.Trends.Performance.Record(row.HourBucket,row.Performance);
                if((row.Availability&32)!=0)snapshot.Trends.ProductiveFacilities.Record(row.HourBucket,row.ProductiveFacilities);
            }
        }
    }
    public partial class IndustrialHistorySaveSystem : GameSystemBase
    {
        protected override void OnUpdate()=>World.GetOrCreateSystemManaged<IndustrialHistorySystem>().Flush();
    }
}
