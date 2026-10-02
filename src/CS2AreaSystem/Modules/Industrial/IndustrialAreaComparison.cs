using System;
using System.Collections.Generic;
using System.Linq;
using CS2AreaSystem.Core.Areas;
namespace CS2AreaSystem.Modules.Industrial
{
    public sealed class IndustrialComparisonSource
    {
        public ulong StableId;public int TypeId;public string Name;
        public AreaLifecycleState Lifecycle;public bool Deleted,Quarantined;
        public IndustrialRuntimeSnapshot Snapshot;
    }
    public sealed class IndustrialComparisonRow
    {
        public ulong StableId;public string Name;public bool Stale;
        public IndustrialMetric Production,Logistics,Employment,Efficiency,Performance;
        public double? PerformanceDelta;
    }
    // Selected-panel read model only. Does not rank or modify the simulation.
    public static class IndustrialAreaComparison
    {
        public const double MaxAgeHours=2;
        public static IReadOnlyList<IndustrialComparisonRow> Build(IEnumerable<IndustrialComparisonSource> sources,double? nowHours)
        {
            var rows=new List<IndustrialComparisonRow>();
            foreach(var source in sources) {
                if(source.TypeId!=(int)AreaTypeId.Industrial||source.Deleted||source.Quarantined||source.StableId==0)continue;
                var snapshot=source.Snapshot;var result=snapshot?.Current;
                bool stale=result!=null&&(source.Lifecycle!=AreaLifecycleState.Active||snapshot.Paused||!result.Active||result.Delayed||
                    !nowHours.HasValue||double.IsNaN(nowHours.Value)||double.IsInfinity(nowHours.Value)||nowHours.Value<result.EndHours||nowHours.Value-result.EndHours>MaxAgeHours);
                IndustrialMetric Read(IndustrialMetric metric)=>stale&&metric.HasValue?metric.AsStale():metric;
                var row=new IndustrialComparisonRow{StableId=source.StableId,Name=source.Name,Stale=stale,
                    Production=Read(result?.Production??default),Logistics=Read(result?.Logistics??default),Employment=Read(result?.Employment??default),
                    Efficiency=Read(result?.Efficiency??default),Performance=Read(result?.Performance??default)};
                if(!stale&&row.Performance.HasValue&&snapshot.Trends.Performance.TryLatest(out var latest)&&latest.Bucket==result.HourBucket&&snapshot.Trends.Performance.TryPrevious(out var previous))
                    row.PerformanceDelta=latest.Value-previous.Value;
                rows.Add(row);
            }
            return rows.OrderByDescending(r=>r.Performance.HasValue).ThenByDescending(r=>r.Performance.HasValue?r.Performance.Value:double.MinValue).ThenBy(r=>r.StableId).ToArray();
        }
    }
}
