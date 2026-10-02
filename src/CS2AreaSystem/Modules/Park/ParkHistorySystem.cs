using System;
using System.Collections.Generic;
using System.Linq;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Diagnostics;
using CS2AreaSystem.Core.Persistence;
using Game;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    public partial class ParkHistorySystem : GameSystemBase
    {
        public const int Capacity=168,AreasPerUpdate=4;
        private ParkRuntimeSystem runtime;private AreaCoreServicesSystem services;
        private readonly List<ParkAreaRef> areas=new List<ParkAreaRef>();private int cursor;
        public override int GetUpdateInterval(SystemUpdatePhase phase)=>256;
        protected override void OnCreate(){base.OnCreate();runtime=World.GetOrCreateSystemManaged<ParkRuntimeSystem>();services=World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();}
        protected override void OnUpdate(){if(!runtime.Ready)return;runtime.CollectParks(areas);for(int i=0;i<Math.Min(AreasPerUpdate,areas.Count);i++)Persist(areas[cursor++%areas.Count]);}
        // Save flush is bounded by managed Areas and 168 elements per Area, never by city entities.
        public void Flush(){if(!runtime.Ready)return;runtime.CollectParks(areas);foreach(var area in areas)Persist(area);}
        private void Persist(ParkAreaRef area)
        {
            try {
                var snapshot=runtime.Store.GetOrCreate(area.StableId,area.Area);
                if(!EntityManager.HasBuffer<ParkHistoryEntry>(area.Area))EntityManager.AddBuffer<ParkHistoryEntry>(area.Area);
                var buffer=EntityManager.GetBuffer<ParkHistoryEntry>(area.Area);var rows=new List<ParkHistoryEntry>(Math.Min(buffer.Length,Capacity));
                for(int i=Math.Max(0,buffer.Length-Capacity);i<buffer.Length;i++){var row=buffer[i];ComponentSchema.RequireSupported(row.SchemaVersion);rows.Add(row);}
                bool changed=buffer.Length>Capacity;
                foreach(var row in Capture(snapshot))changed|=Record(rows,row);
                if(changed){buffer.Clear();foreach(var row in rows)buffer.Add(row);}
                services.Failures.Recovered(area.Area,"Park.History");
            }catch(Exception error){services.Failures.Report(area.Area,"Park.History",error,"Area history unavailable; gameplay continues without overwriting unsupported history");}
        }
        public static bool Record(IList<ParkHistoryEntry> rows,ParkHistoryEntry next)
        {
            ComponentSchema.RequireSupported(next.SchemaVersion==0?ParkHistoryEntry.CurrentSchemaVersion:next.SchemaVersion);
            next.SchemaVersion=1;
            for(int i=rows.Count-1;i>=0;i--)if(rows[i].HourBucket==next.HourBucket){var merged=rows[i];
                if((next.Availability&1)!=0)merged.Visitors=next.Visitors;
                if((next.Availability&2)!=0)merged.Quality=next.Quality;
                if((next.Availability&4)!=0)merged.Satisfaction=next.Satisfaction;
                if((next.Availability&8)!=0)merged.Occupancy=next.Occupancy;
                if((next.Availability&16)!=0)merged.AdmissionIncome=next.AdmissionIncome;
                if((next.Availability&32)!=0)merged.ManagementExpense=next.ManagementExpense;
                merged.SchemaVersion=1;merged.Availability|=next.Availability;if(rows[i].Equals(merged))return false;rows[i]=merged;return true;}
            if(rows.Count>0&&next.HourBucket<rows[rows.Count-1].HourBucket)return false;
            rows.Add(next);while(rows.Count>Capacity)rows.RemoveAt(0);return true;
        }
        public static List<ParkHistoryEntry> Capture(ParkRuntimeSnapshot snapshot)
        {
            var rows=new SortedDictionary<long,ParkHistoryEntry>();var samples=new List<RuntimeTrendSample>(24);
            snapshot.Trends.Visitors.CopyTo(samples);
            foreach(var sample in samples) { rows.TryGetValue(sample.Bucket,out var row);row.SchemaVersion=1;row.HourBucket=sample.Bucket;row.Availability|=1;row.Visitors=sample.Value;rows[sample.Bucket]=row; }
            snapshot.Trends.Quality.CopyTo(samples);
            foreach(var sample in samples) { rows.TryGetValue(sample.Bucket,out var row);row.SchemaVersion=1;row.HourBucket=sample.Bucket;row.Availability|=2;row.Quality=sample.Value;rows[sample.Bucket]=row; }
            snapshot.Trends.Satisfaction.CopyTo(samples);
            foreach(var sample in samples) { rows.TryGetValue(sample.Bucket,out var row);row.SchemaVersion=1;row.HourBucket=sample.Bucket;row.Availability|=4;row.Satisfaction=sample.Value;rows[sample.Bucket]=row; }
            snapshot.Trends.Occupancy.CopyTo(samples);
            foreach(var sample in samples) { rows.TryGetValue(sample.Bucket,out var row);row.SchemaVersion=1;row.HourBucket=sample.Bucket;row.Availability|=8;row.Occupancy=sample.Value;rows[sample.Bucket]=row; }
            snapshot.Trends.AdmissionIncome.CopyTo(samples);
            foreach(var sample in samples) { rows.TryGetValue(sample.Bucket,out var row);row.SchemaVersion=1;row.HourBucket=sample.Bucket;row.Availability|=16;row.AdmissionIncome=sample.Value;rows[sample.Bucket]=row; }
            snapshot.Trends.ManagementExpense.CopyTo(samples);
            foreach(var sample in samples) { rows.TryGetValue(sample.Bucket,out var row);row.SchemaVersion=1;row.HourBucket=sample.Bucket;row.Availability|=32;row.ManagementExpense=sample.Value;rows[sample.Bucket]=row; }
            return rows.Values.ToList();
        }
        public static void Seed(EntityManager entities,ParkRuntimeSnapshot snapshot)
        {
            if(!entities.HasBuffer<ParkHistoryEntry>(snapshot.Area))return;
            var buffer=entities.GetBuffer<ParkHistoryEntry>(snapshot.Area,true);var rows=new List<ParkHistoryEntry>(24);
            for(int i=Math.Max(0,buffer.Length-24);i<buffer.Length;i++)rows.Add(buffer[i]);
            Seed(rows,snapshot);
        }
        public static void Seed(IEnumerable<ParkHistoryEntry> history,ParkRuntimeSnapshot snapshot)
        {
            // Loaded buffers have a fixed writer bound. TakeLast prevents old buckets inflating runtime series.
            var newest=new Queue<ParkHistoryEntry>();
            foreach(var row in history){newest.Enqueue(row);if(newest.Count>24)newest.Dequeue();}
            foreach(var row in newest) {
                ComponentSchema.RequireSupported(row.SchemaVersion==0?ParkHistoryEntry.CurrentSchemaVersion:row.SchemaVersion);
                if((row.Availability&1)!=0)snapshot.Trends.Visitors.Record(row.HourBucket,row.Visitors);
                if((row.Availability&2)!=0)snapshot.Trends.Quality.Record(row.HourBucket,row.Quality);
                if((row.Availability&4)!=0)snapshot.Trends.Satisfaction.Record(row.HourBucket,row.Satisfaction);
                if((row.Availability&8)!=0)snapshot.Trends.Occupancy.Record(row.HourBucket,row.Occupancy);
                if((row.Availability&16)!=0)snapshot.Trends.AdmissionIncome.Record(row.HourBucket,row.AdmissionIncome);
                if((row.Availability&32)!=0)snapshot.Trends.ManagementExpense.Record(row.HourBucket,row.ManagementExpense);
            }
        }
    }
    public partial class ParkHistorySaveSystem : GameSystemBase
    {
        protected override void OnUpdate()=>World.GetOrCreateSystemManaged<ParkHistorySystem>().Flush();
    }
}
