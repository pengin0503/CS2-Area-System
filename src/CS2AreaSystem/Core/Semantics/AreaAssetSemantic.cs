using System;
namespace CS2AreaSystem.Core.Semantics
{
    [Flags]
    public enum AreaAssetSemantic : ushort
    {
        None=0,ParkEntrance=1,ParkHeadquarters=2,ParkLeisure=4,ParkAttraction=8,
        IndustrialHeadquarters=16,IndustrialExtractor=32,IndustrialProducer=64,IndustrialStorage=128,IndustrialCargo=256
    }
    public readonly struct AreaAssetSemanticPatch
    {
        public AreaAssetSemantic Allow { get; } public AreaAssetSemantic Deny { get; } public int Priority { get; }
        public AreaAssetSemanticPatch(AreaAssetSemantic allow,AreaAssetSemantic deny,int priority){Allow=allow;Deny=deny;Priority=priority;}
    }
}
