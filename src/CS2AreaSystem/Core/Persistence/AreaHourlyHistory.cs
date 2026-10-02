using System;
using System.Collections.Generic;
using System.Linq;
namespace CS2AreaSystem.Core.Persistence
{
    // Normalize before retaining/seeding: duplicate physical rows must not displace distinct hours.
    // Later rows replace only their available channels. Memory is bounded even for an oversized input.
    internal static class AreaHourlyHistory
    {
        public static List<T> Normalize<T>(IEnumerable<T> history, Func<T,long> hour,
            Func<T,T,T> merge, Action<T> validate, int capacity)
        {
            var buckets=new SortedDictionary<long,T>();
            foreach(var row in history) {
                validate(row);
                var key=hour(row);
                buckets[key]=merge(buckets.TryGetValue(key,out var previous)?previous:default,row);
                if(buckets.Count>capacity)buckets.Remove(buckets.Keys.First());
            }
            return buckets.Values.ToList();
        }
    }
}
