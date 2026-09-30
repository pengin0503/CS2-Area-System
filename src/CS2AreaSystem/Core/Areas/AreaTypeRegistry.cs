using System;
using System.Collections.Generic;
namespace CS2AreaSystem.Core.Areas
{
    public sealed class AreaTypeRegistry
    {
        private readonly Dictionary<int, AreaTypeDescriptor> types = new Dictionary<int, AreaTypeDescriptor>();
        public IReadOnlyCollection<AreaTypeDescriptor> All => types.Values;
        public bool Register(AreaTypeDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (types.ContainsKey(descriptor.TypeId)) return false;
            types.Add(descriptor.TypeId, descriptor);
            return true;
        }
        public bool TryGet(int typeId, out AreaTypeDescriptor descriptor) => types.TryGetValue(typeId, out descriptor);
    }
}
