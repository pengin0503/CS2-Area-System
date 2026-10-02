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
                var buffer=EntityManager.GetBuffer<ParkHistoryEntry>(area.Area);
                if(buffer.Length>Capacity)throw new System.IO.InvalidDataException("Oversized Area history buffer; not overwritten");
                var loaded=new List<ParkHistoryEntry>(buffer.Length);
                for(int i=0;i<buffer.Length;i++){var row=buffer[i];ComponentSchema.RequireSupported(row.SchemaVersion);loaded.Add(row);}
                var rows=Normalize(loaded);bool changed=!loaded.SequenceEqual(rows);
                foreach(var row in Capture(snapshot))changed|=Record(rows,row);
                if(changed){buffer.Clear();foreach(var row in rows)buffer.Add(row);}
                services.Failures.Recovered(area.Area,"Park.History");
            }catch(Exception error){services.Failures.Report(area.Area,"Park.History",error,"Area history unavailable; gameplay continues without overwriting unsupported history");}
        }
        private static ParkHistoryEntry Merge(ParkHistoryEntry previous,ParkHistoryEntry next)
        {
            previous.SchemaVersion=1;previous.HourBucket=next.HourBucket;
            if((next.Availability&1)!=0)previous.Visitors=next.Visitors;
            if((next.Availability&2)!=0)previous.Quality=next.Quality;
            if((next.Availability&4)!=0)previous.Satisfaction=next.Satisfaction;
            if((next.Availability&8)!=0)previous.Occupancy=next.Occupancy;
            if((next.Availability&16)!=0)previous.AdmissionIncome=next.AdmissionIncome;
            if((next.Availability&32)!=0)previous.ManagementExpense=next.ManagementExpense;
            previous.Availability|=next.Availability;return previous;
        }
        private static List<ParkHistoryEntry> Normalize(IEnumerable<ParkHistoryEntry> history) =>
            AreaHourlyHistory.Normalize(history,row=>row.HourBucket,Merge,
                row=>ComponentSchema.RequireSupported(row.SchemaVersion==0?ParkHistoryEntry.CurrentSchemaVersion:row.SchemaVersion),Capacity);
        public static bool Record(IList<ParkHistoryEntry> rows,ParkHistoryEntry next)
        {
            ComponentSchema.RequireSupported(next.SchemaVersion==0?ParkHistoryEntry.CurrentSchemaVersion:next.SchemaVersion);
            var normalized=Normalize(rows);bool changed=!rows.SequenceEqual(normalized);
            int index=normalized.FindIndex(row=>row.HourBucket==next.HourBucket);
            if(index>=0){var merged=Merge(normalized[index],next);changed|=!normalized[index].Equals(merged);normalized[index]=merged;}
            else if(normalized.Count==0||next.HourBucket>=normalized[normalized.Count-1].HourBucket){normalized.Add(Merge(default,next));changed=true;}
            while(normalized.Count>Capacity)normalized.RemoveAt(0);
            if(changed){rows.Clear();foreach(var row in normalized)rows.Add(row);}
            return changed;
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
            var buffer=entities.GetBuffer<ParkHistoryEntry>(snapshot.Area,true);
            if(buffer.Length>Capacity)throw new System.IO.InvalidDataException("Oversized Area history buffer");
            var rows=new List<ParkHistoryEntry>(buffer.Length);
            for(int i=0;i<buffer.Length;i++)rows.Add(buffer[i]);
            Seed(rows,snapshot);
        }
        public static void Seed(IEnumerable<ParkHistoryEntry> history,ParkRuntimeSnapshot snapshot)
        {
            var normalized=Normalize(history);
            foreach(var row in normalized.Skip(Math.Max(0,normalized.Count-24))) {
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
