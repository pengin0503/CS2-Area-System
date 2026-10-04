using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Semantics;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    // Phase 3 C2: a Park needs a valid AS_ParkHeadquarters and a valid AS_ParkGate; a saved link that no longer qualifies
    // (for example a Foundation Park Maintenance HQ) is reported as invalid rather than missing, and a role whose AS_
    // package is not installed is reported as missing content first, because nothing can be assigned until it is.
    public static class ParkActivationSummary
    {
        public static string Key(bool headquarters,bool headquartersLinked,bool entrance,bool entranceLinked) =>
            Key(headquarters?ParkHeadquartersState.Valid:headquartersLinked?ParkHeadquartersState.Invalid:ParkHeadquartersState.Missing,
                entrance?ParkHeadquartersState.Valid:entranceLinked?ParkHeadquartersState.Invalid:ParkHeadquartersState.Missing);
        public static string Key(ParkHeadquartersState headquarters,ParkHeadquartersState entrance)
        {
            const string P="CS2AS.Park.Summary.";
            bool hq=headquarters==ParkHeadquartersState.Valid,gate=entrance==ParkHeadquartersState.Valid;
            if(hq&&gate)return P+"Active";
            bool hqContent=headquarters==ParkHeadquartersState.ContentMissing,gateContent=entrance==ParkHeadquartersState.ContentMissing;
            if(hqContent&&gateContent)return P+"ContentMissing";
            if(hqContent)return P+"HeadquartersContentMissing";
            if(gateContent)return P+"GateContentMissing";
            if(!hq&&!gate)return P+"BothRequired";
            if(!hq)return P+(headquarters==ParkHeadquartersState.Invalid?"HeadquartersInvalid":"HeadquartersRequired");
            return P+(entrance==ParkHeadquartersState.Invalid?"EntranceInvalid":"EntranceRequired");
        }
        // Live state of one dedicated role. A null registry makes no content claim (Missing/Invalid only).
        public static ParkHeadquartersState State(AreaAnchorService anchors,AreaAssetSemanticRegistry semantics,Entity area,AreaAnchorRole role)
        {
            if(anchors.HasValidAnchor(area,role))return ParkHeadquartersState.Valid;
            var semantic=role==AreaAnchorRole.Headquarters?AreaAssetSemantic.ParkHeadquarters:AreaAssetSemantic.ParkEntrance;
            if(semantics!=null&&!AreaBuiltInAssetContract.IsContentPresent(semantics,semantic))return ParkHeadquartersState.ContentMissing;
            return anchors.HasLinks(area,role)?ParkHeadquartersState.Invalid:ParkHeadquartersState.Missing;
        }
    }
}
