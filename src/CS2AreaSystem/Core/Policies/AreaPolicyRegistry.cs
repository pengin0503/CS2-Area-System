using System;
using System.Collections.Generic;
using System.Linq;
namespace CS2AreaSystem.Core.Policies
{
    public sealed class AreaPolicyRegistry
    {
        private readonly Dictionary<int, AreaPolicyDefinition> definitions = new Dictionary<int, AreaPolicyDefinition>();
        public IReadOnlyCollection<AreaPolicyDefinition> All => definitions.Values;
        // A duplicate ID is rejected and never replaces the first definition (released IDs are immutable).
        public bool Register(AreaPolicyDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definitions.ContainsKey(definition.PolicyId)) return false;
            definitions.Add(definition.PolicyId, definition);
            return true;
        }
        public bool TryGet(int policyId, out AreaPolicyDefinition definition) => definitions.TryGetValue(policyId, out definition);
        public IReadOnlyList<AreaPolicyDefinition> ForAreaType(int areaTypeId) =>
            definitions.Values.Where(d => d.Supports(areaTypeId)).OrderBy(d => d.PolicyId).ToArray();
    }
}
