namespace CS2AreaSystem.Modules.Park
{
    // Phase 3 C2: a Park needs a valid AS_ParkHeadquarters and a valid AS_ParkGate; a saved link that no longer qualifies
    // (for example a Foundation Park Maintenance HQ) is reported as invalid rather than missing.
    public static class ParkActivationSummary
    {
        public static string Key(bool headquarters,bool headquartersLinked,bool entrance,bool entranceLinked)
        {
            if(headquarters&&entrance)return "CS2AS.Park.Summary.Active";
            if(!headquarters&&!entrance)return "CS2AS.Park.Summary.BothRequired";
            if(!headquarters)return "CS2AS.Park.Summary."+(headquartersLinked?"HeadquartersInvalid":"HeadquartersRequired");
            return "CS2AS.Park.Summary."+(entranceLinked?"EntranceInvalid":"EntranceRequired");
        }
    }
}
