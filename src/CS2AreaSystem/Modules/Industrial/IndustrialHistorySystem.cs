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
                var buffer=EntityManager.GetBuffer<IndustrialHistoryEntry>(area.Area);var rows=new List<IndustrialHistoryEntry>(Math.Min(buffer.Length,Capacity));
                for(int i=Math.Max(0,buffer.Length-Capacity);i<buffer.Length;i++){var row=buffer[i];ComponentSchema.RequireSupported(row.SchemaVersion);rows.Add(row);}
                bool changed=buffer.Length>Capacity;
                foreach(var row in Capture(snapshot))changed|=Record(rows,row);
                if(changed){buffer.Clear();foreach(var row in rows)buffer.Add(row);}
                services.Failures.Recovered(area.Area,"Industrial.History");
            }catch(Exception error){services.Failures.Report(area.Area,"Industrial.History",error,"Area history unavailable; gameplay continues without overwriting unsupported history");}
        }
        public static bool Record(IList<IndustrialHistoryEntry> rows,IndustrialHistoryEntry next)
        {
            ComponentSchema.RequireSupported(next.SchemaVersion==0?IndustrialHistoryEntry.CurrentSchemaVersion:next.SchemaVersion);
            next.SchemaVersion=1;
            for(int i=rows.Count-1;i>=0;i--)if(rows[i].HourBucket==next.HourBucket){var merged=rows[i];
                if((next.Availability&1)!=0)merged.Production=next.Production;
                if((next.Availability&2)!=0)merged.Logistics=next.Logistics;
                if((next.Availability&4)!=0)merged.Employment=next.Employment;
                if((next.Availability&8)!=0)merged.Efficiency=next.Efficiency;
                if((next.Availability&16)!=0)merged.Performance=next.Performance;
                if((next.Availability&32)!=0)merged.ProductiveFacilities=next.ProductiveFacilities;
                merged.SchemaVersion=1;merged.Availability|=next.Availability;if(rows[i].Equals(merged))return false;rows[i]=merged;return true;}
            if(rows.Count>0&&next.HourBucket<rows[rows.Count-1].HourBucket)return false;
            rows.Add(next);while(rows.Count>Capacity)rows.RemoveAt(0);return true;
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
            var buffer=entities.GetBuffer<IndustrialHistoryEntry>(snapshot.Area,true);var rows=new List<IndustrialHistoryEntry>(24);
            for(int i=Math.Max(0,buffer.Length-24);i<buffer.Length;i++)rows.Add(buffer[i]);
            Seed(rows,snapshot);
        }
        public static void Seed(IEnumerable<IndustrialHistoryEntry> history,IndustrialRuntimeSnapshot snapshot)
        {
            // Loaded buffers have a fixed writer bound. TakeLast prevents old buckets inflating runtime series.
            var newest=new Queue<IndustrialHistoryEntry>();
            foreach(var row in history){newest.Enqueue(row);if(newest.Count>24)newest.Dequeue();}
            foreach(var row in newest) {
                ComponentSchema.RequireSupported(row.SchemaVersion==0?IndustrialHistoryEntry.CurrentSchemaVersion:row.SchemaVersion);
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
