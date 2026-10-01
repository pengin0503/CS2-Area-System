using System.Collections.Generic;
namespace CS2AreaSystem.Core.Policies
{
    // Core policy texts: the common policy, lock/unavailable reasons and the policy section's labels.
    public static class AreaPolicyLocalization
    {
        public static void Append(Dictionary<string, string> e, bool ja)
        {
            void Add(string key, string en, string jp) => e[key] = ja ? jp : en;
            Add("Policy.TITLE[CS2AS.Policy.FocusedManagement]", "Focused Management", "重点管理");
            Add("Policy.DESCRIPTION[CS2AS.Policy.FocusedManagement]",
                "Area XP from awards the Area already qualifies for is increased by 10%. It never bypasses earning requirements or daily XP caps.",
                "エリアが既に獲得条件を満たしている経験値を10%増やします。獲得条件や1日の経験値上限を超えることはありません。");
            Add(AreaPolicyReasons.Unknown, "This policy's module is not installed. The saved setting is kept.", "この条例のモジュールが見つかりません。保存された設定は保持されます。");
            Add(AreaPolicyReasons.UnsupportedAreaType, "Not available for this Area type", "このエリアの種類では利用できません");
            Add(AreaPolicyReasons.Locked, "Requires a higher Area Level", "より高いエリアレベルが必要です");
            Add(AreaPolicyReasons.EffectUnavailable, "Effect not available in this game version", "このゲームバージョンでは効果を利用できません");
            Add(AreaPolicyReasons.Suspended, "Paused while the Area is suspended", "エリアの停止中は一時停止しています");
            Add(AreaPolicyReasons.Conflict, "Overridden by a conflicting policy", "競合する条例により無効です");
            Add(AreaPolicyReasons.AreaUnavailable, "Area unavailable", "エリアを利用できません");
            for (int level = 1; level <= 5; level++)
                Add("CS2AS.Policy.Requirement.Level" + level, "Requires Area Level " + level, "エリアレベル" + level + "が必要");
            Add("CS2AS.Policy.Status.Active", "In effect", "施行中");
            Add("CS2AS.Policy.Status.On", "On", "オン");
            Add("CS2AS.Policy.Status.Off", "Off", "オフ");
            Add("CS2AS.Policy.On", "On", "オン");
            Add("CS2AS.Policy.Off", "Off", "オフ");
            Add("CS2AS.Policy.PerDay", "/day", "/日");
        }
    }
}
