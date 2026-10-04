using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
namespace CS2AreaSystem.Core.Semantics
{
    // Public contract v1. Registrations are runtime prefab identities for this World/load, never saved entity IDs.
    // Providers must re-register after load; removal affects only the named provider.
    public sealed class AreaAssetSemanticRegistry
    {
        public const int ApiVersion=1;
        private const AreaAssetSemantic Known=(AreaAssetSemantic)511;
        private const int CacheCapacity=4096;
        private readonly object gate=new object();private long revision;
        private readonly Dictionary<Entity,Dictionary<string,AreaAssetSemanticPatch>> patches=new Dictionary<Entity,Dictionary<string,AreaAssetSemanticPatch>>();
        private readonly Dictionary<(Entity,AreaAssetSemantic,bool,long),bool> cache=new Dictionary<(Entity,AreaAssetSemantic,bool,long),bool>();
        public long Revision { get {lock(gate)return revision;} }
        public bool Register(string providerId,Entity prefab,AreaAssetSemanticPatch patch)
        {
            if(string.IsNullOrWhiteSpace(providerId)||prefab==Entity.Null||(patch.Allow&patch.Deny)!=0||((patch.Allow|patch.Deny)&~Known)!=0)return false;
            lock(gate){if(!patches.TryGetValue(prefab,out var providers))patches[prefab]=providers=new Dictionary<string,AreaAssetSemanticPatch>(StringComparer.Ordinal);
                providers[providerId]=patch;Changed();return true;}
        }
        public bool Unregister(string providerId,Entity prefab)
        {
            if(providerId==null)return false;
            lock(gate){if(!patches.TryGetValue(prefab,out var providers)||!providers.Remove(providerId))return false;if(providers.Count==0)patches.Remove(prefab);Changed();return true;}
        }
        public int UnregisterProvider(string providerId)
        {
            if(providerId==null)return 0;
            lock(gate){int count=0;foreach(var pair in patches.ToArray()){if(pair.Value.Remove(providerId))count++;if(pair.Value.Count==0)patches.Remove(pair.Key);}if(count>0)Changed();return count;}
        }
        public bool Resolve(Entity prefab,AreaAssetSemantic semantic,bool vanillaDefault)
        {
            lock(gate){var key=(prefab,semantic,vanillaDefault,revision);if(cache.TryGetValue(key,out var value))return value;
                value=TryWinner(prefab,semantic,out var winner)?(winner.Allow&semantic)!=0:vanillaDefault;
                if(cache.Count>=CacheCapacity)cache.Clear();cache[key]=value;return value;}
        }
        public bool HasRegisteredAllow(AreaAssetSemantic semantic)
        {
            lock(gate){foreach(var prefab in patches.Keys)if(TryWinner(prefab,semantic,out var winner)&&(winner.Allow&semantic)!=0)return true;return false;}
        }
        // True when the named provider's own patch for this prefab allows the semantic (independently of precedence).
        public bool HasProviderAllow(string providerId,Entity prefab,AreaAssetSemantic semantic)
        {
            if(providerId==null)return false;
            lock(gate)return patches.TryGetValue(prefab,out var providers)&&providers.TryGetValue(providerId,out var patch)&&(patch.Allow&semantic)!=0;
        }
        public bool HasProviderAllow(string providerId,AreaAssetSemantic semantic)
        {
            if(providerId==null)return false;
            lock(gate){foreach(var providers in patches.Values)if(providers.TryGetValue(providerId,out var patch)&&(patch.Allow&semantic)!=0)return true;return false;}
        }
        private bool TryWinner(Entity prefab,AreaAssetSemantic semantic,out AreaAssetSemanticPatch winner)
        {
            winner=default;int bit=(int)semantic;if(bit<=0||(bit&(bit-1))!=0||(semantic&~Known)!=0)return false;
            if(!patches.TryGetValue(prefab,out var providers))return false;string chosen=null;
            foreach(var p in providers){if(((p.Value.Allow|p.Value.Deny)&semantic)==0)continue;
                if(chosen==null||p.Value.Priority>winner.Priority||p.Value.Priority==winner.Priority&&StringComparer.Ordinal.Compare(p.Key,chosen)<0){chosen=p.Key;winner=p.Value;}}
            return chosen!=null;
        }
        public void Clear(){lock(gate){patches.Clear();Changed();}}
        private void Changed(){revision++;cache.Clear();}
    }
}
