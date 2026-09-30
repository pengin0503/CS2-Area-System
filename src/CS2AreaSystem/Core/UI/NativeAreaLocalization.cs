using System.Collections.Generic;
namespace CS2AreaSystem.Core.UI
{
    public static class NativeAreaLocalization
    {
        public static Dictionary<string,string> Entries(string locale)
        {
            bool ja = locale == "ja-JP";
            var entries = new Dictionary<string,string> {
                ["Assets.NAME[CS2AS Areas Menu]"] = ja ? "エリア" : "Areas",
                ["Assets.NAME[CS2AS Area Types]"] = ja ? "エリアの種類" : "Area Types",
                ["Assets.NAME[CS2AS.ParkArea]"] = ja ? "公園エリア" : "Park Area",
                ["Assets.NAME[CS2AS.IndustrialArea]"] = ja ? "産業エリア" : "Industrial Area",
                ["Assets.NAME[CS2AS.TourismArea]"] = ja ? "観光エリア" : "Tourism Area"
            };
            return entries;
        }
    }
}
