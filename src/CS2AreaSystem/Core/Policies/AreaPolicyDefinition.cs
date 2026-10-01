using System;
using System.Collections.Generic;
using System.Linq;
namespace CS2AreaSystem.Core.Policies
{
    public enum AreaPolicyEffectClass : byte { AreaInternal, VanillaSimulation }
    public sealed class AreaPolicyDefinition
    {
        public const int MinAreaLevel = 1, MaxAreaLevel = 5;
        private readonly int[] areaTypeIds;
        public int PolicyId { get; }
        public string NameKey { get; }
        public string DescriptionKey { get; }
        public string IconUri { get; }
        public IReadOnlyCollection<int> AreaTypeIds => areaTypeIds;
        public int MinLevel { get; }
        public AreaPolicyUpkeep Upkeep { get; }
        // Explicit mutual exclusion key; null means the policy conflicts with nothing.
        public string ConflictGroup { get; }
        public AreaPolicyEffectClass EffectClass { get; }
        public string EffectHandlerKey { get; }
        public AreaPolicyDefinition(int policyId, string nameKey, string descriptionKey, string iconUri, IEnumerable<int> areaTypeIds,
            int minLevel, AreaPolicyUpkeep upkeep, string conflictGroup, AreaPolicyEffectClass effectClass, string effectHandlerKey)
        {
            if (policyId <= 0) throw new ArgumentOutOfRangeException(nameof(policyId));
            if (minLevel < MinAreaLevel || minLevel > MaxAreaLevel) throw new ArgumentOutOfRangeException(nameof(minLevel));
            upkeep.Validate();
            this.areaTypeIds = (areaTypeIds ?? throw new ArgumentNullException(nameof(areaTypeIds))).Distinct().ToArray();
            if (this.areaTypeIds.Length == 0) throw new ArgumentException("A policy supports at least one Area type.", nameof(areaTypeIds));
            if (conflictGroup != null && string.IsNullOrWhiteSpace(conflictGroup)) throw new ArgumentException("Conflict group must be null or a key.", nameof(conflictGroup));
            PolicyId = policyId;
            NameKey = RequireKey(nameKey, nameof(nameKey));
            DescriptionKey = RequireKey(descriptionKey, nameof(descriptionKey));
            IconUri = iconUri;
            MinLevel = minLevel;
            Upkeep = upkeep;
            ConflictGroup = conflictGroup;
            EffectClass = effectClass;
            EffectHandlerKey = RequireKey(effectHandlerKey, nameof(effectHandlerKey));
        }
        public bool Supports(int areaTypeId) => Array.IndexOf(areaTypeIds, areaTypeId) >= 0;
        private static string RequireKey(string value, string name) =>
            string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Required key is missing.", name) : value;
    }
}
