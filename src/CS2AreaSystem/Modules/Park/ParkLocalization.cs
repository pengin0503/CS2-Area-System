using System.Collections.Generic;
namespace CS2AreaSystem.Modules.Park
{
    public static class ParkLocalization
    {
        public static void Append(Dictionary<string, string> e, bool ja)
        {
            void Add(string key, string en, string jp) => e[key] = ja ? jp : en;
            const string P = "CS2AS.Park.";
            Add("CS2AS.Tab.Visitors", "Visitors", "来園者");
            Add("CS2AS.Tab.Finances", "Finances", "財政");
            Add("CS2AS.Tab.Landscape", "Landscape", "景観");
            Add("CS2AS.Tab.Progress", "Progress", "進捗");
            Add(P + "Summary.Active", "Open. Visits are measured from real residents' and tourists' leisure trips.", "開園中。来園者は住民と観光客の実際の余暇行動から計測されます。");
            Add(P + "Summary.EntranceRequired", "Assign a park, leisure or attraction building inside this area as an entrance to open the Park.", "エリア内の公園・余暇施設・観光名所の建物を入口に割り当てると開園します。");
            Add(P + "Summary.EntranceInvalid", "Suspended: none of the assigned entrances qualifies any more. Remove the marked entrances and assign a park, leisure or attraction building inside this area.", "停止中：割り当て済みの入口がどれも条件を満たしていません。印の付いた入口を解除し、エリア内の公園・余暇施設・観光名所の建物を入口に割り当ててください。");
            Add(P + "LevelName.1", "Local Park", "地域公園");
            Add(P + "LevelName.2", "Neighborhood Park", "近隣公園");
            Add(P + "LevelName.3", "City Park", "都市公園");
            Add(P + "LevelName.4", "Major Park", "大規模公園");
            Add(P + "LevelName.5", "Landmark Park", "ランドマーク公園");
            Add(P + "Level", "Park level", "公園レベル");
            Add(P + "Experience", "Experience", "経験値");
            Add(P + "Quality", "Park Quality", "公園品質");
            Add(P + "Quality.Tip", "Satisfaction 25%, Attractiveness 20%, Accessibility 15%, Variety 15%, Maintenance 15%, Comfort 10%.", "満足度25%・魅力20%・アクセス15%・多様性15%・維持管理15%・快適性10%。");
            Add(P + "Calculating", "Calculating…", "計算中…");
            Add(P + "Unavailable", "Unavailable (counted as neutral)", "利用不可（中立値で計算）");
            Add(P + "Metric.Satisfaction", "Visitor satisfaction", "来園者満足度");
            Add(P + "Metric.Satisfaction.Tip", "Rolling average of completed visits; starts near 50 until enough visits are observed.", "完了した来園の移動平均です。十分な来園が観測されるまでは50付近から始まります。");
            Add(P + "Metric.Attractiveness", "Attractiveness", "魅力");
            Add(P + "Metric.Attractiveness.Tip", "About 60% real facility attractiveness and 40% landscape.", "実際の施設の魅力 約60%と景観 約40%。");
            Add(P + "Metric.Accessibility", "Accessibility", "アクセス");
            Add(P + "Metric.Accessibility.Tip", "Entrances (and how spread out they are), nearby public transport, footpaths inside the Park and footpaths reaching each entrance.", "入口（数と配置の広がり）、近くの公共交通、園内の歩道、各入口につながる歩道。");
            Add(P + "Metric.Variety", "Variety", "多様性");
            Add(P + "Metric.Variety.Tip", "Different kinds of leisure facilities and landscape categories. Breadth beats repetition.", "余暇施設と景観カテゴリーの種類の幅。同じものを増やすより種類を増やす方が効果的です。");
            Add(P + "Metric.Maintenance", "Maintenance", "維持管理");
            Add(P + "Metric.Maintenance.Tip", "Condition of the Park's vanilla park buildings plus area management funded by the Park budget.", "公園施設の状態と、公園予算による管理の充足度。");
            Add(P + "Metric.Comfort", "Comfort", "快適性");
            Add(P + "Metric.Comfort.Tip", "Crowding relative to the estimated comfortable capacity. Popularity only hurts above that capacity.", "推定快適収容人数に対する混雑度。収容人数を超えるまでは人気は不利になりません。");
            Add(P + "ActiveVisitors", "Visitors now", "現在の来園者");
            Add(P + "RecentVisitors", "Visits (last day)", "来園数（直近1日）");
            Add(P + "Recent.Tip", "Last in-game day. Recent statistics restart after loading a save.", "ゲーム内の直近1日。直近の統計はセーブの読み込み後に再集計されます。");
            Add(P + "Entrances", "Entrances", "入口");
            Add(P + "NextAction", "Suggested next step", "次のおすすめ");
            Add(P + "Warning.NoEntrance", "No valid entrance: the Park is suspended. Level, XP and statistics are kept.", "有効な入口がないため停止中です。レベル・経験値・統計は保持されます。");
            Add(P + "Warning.NotPrimary", "Overlaps another Park: visitors at shared buildings are counted only by the other Park.", "他の公園エリアと重なっています。共有する建物の来園者は他方の公園でのみ計上されます。");
            Add(P + "Warning.LowConfidence", "Some quality data is not available yet; a neutral value is used.", "一部の品質データがまだ利用できないため中立値を使用しています。");
            Add(P + "Warning.NoFacilities", "No park, leisure or attraction buildings inside: nobody can visit yet.", "公園・余暇施設・観光名所の建物がないため、まだ来園できません。");
            Add(P + "Warning.Crowded", "Over comfortable capacity: visitors feel crowded.", "快適収容人数を超えて混雑しています。");
            Add(P + "Warning.LowMaintenance", "Poor maintenance: raise the Park budget or look after park buildings.", "維持管理が不十分です。公園予算を上げるか公園施設を確認してください。");
            Add(P + "Warning.NoPaths", "No footpaths inside the Park.", "園内に歩道がありません。");
            Add(P + "Warning.Cluttered", "Too many objects packed together: a clutter penalty applies.", "物が密集しすぎているため、過密ペナルティがかかっています。");
            Add(P + "Warning.PoorValue", "The admission fee feels too high for this Park's quality.", "公園の品質に対して入園料が高すぎると感じられています。");
            Add(P + "Action.Entrance", "Assign an entrance", "入口を割り当てる");
            Add(P + "Action.Attractiveness", "Add attractions or improve the landscape", "観光名所を追加するか景観を改善する");
            Add(P + "Action.Accessibility", "Add entrances, footpaths or a nearby transit stop", "入口・歩道・近くの停留所を追加する");
            Add(P + "Action.Variety", "Add different kinds of facilities and landscaping", "種類の異なる施設や景観を追加する");
            Add(P + "Action.Maintenance", "Raise the Park budget", "公園予算を上げる");
            Add(P + "Action.Comfort", "Expand the Park or add paths and amenities", "公園を広げるか歩道や休憩設備を追加する");
            Add(P + "Action.Satisfaction", "Improve the visitor experience (and check the fee)", "来園体験を改善する（入園料も確認）");
            Add(P + "Action.Wait", "Keep the Park running", "このまま運営を続ける");
            Add(P + "LifetimeVisitors", "Total visits", "累計来園数");
            Add(P + "ResidentVisitors", "Resident visits", "住民の来園数");
            Add(P + "TouristVisitors", "Tourist visits", "観光客の来園数");
            Add(P + "ResidentSatisfaction", "Resident satisfaction", "住民の満足度");
            Add(P + "TouristSatisfaction", "Tourist satisfaction", "観光客の満足度");
            Add(P + "Note.SatisfactionPrior", "Few visits so far: satisfaction is still close to the neutral starting value.", "来園がまだ少ないため、満足度は中立の初期値に近い値です。");
            Add(P + "Capacity", "Comfortable capacity", "快適収容人数");
            Add(P + "Capacity.Tip", "Estimated from area, footpaths, facilities and amenities. Park level does not change it.", "面積・歩道・施設・休憩設備から推定します。公園レベルでは変わりません。");
            Add(P + "Occupancy", "Occupancy", "混雑度");
            Add(P + "PeakOccupancy", "Peak visitors", "最大来園者数");
            Add(P + "TourismContribution", "Tourism contribution", "観光貢献度");
            Add(P + "TourismContribution.Tip", "Derived value for a future Tourism Area. It does not change vanilla tourism.", "将来の観光エリア向けの派生値です。ゲーム本来の観光には影響しません。");
            Add(P + "Budget", "Park budget", "公園予算");
            Add(P + "Budget.Tip", "100% funds the estimated area management need. Lower budgets reduce Maintenance; higher budgets help with diminishing returns.", "100%で推定される管理費を賄います。下げると維持管理が低下し、上げると効果は逓減しつつ改善します。");
            Add(P + "Fee", "Admission fee", "入園料");
            Add(P + "Fee.Tip", "Charged once per visit from the visitor's household money to the city. It does not change where citizens go.", "来園ごとに1回、来園者の世帯のお金から市に支払われます。市民の行き先は変わりません。");
            Add(P + "Fee.Locked", "Admission unlocks at Neighborhood Park (level 2).", "入園料は近隣公園（レベル2）で解放されます。");
            Add(P + "Free", "Free", "無料");
            Add(P + "RecentAdmission", "Admission income (last day)", "入園料収入（直近1日）");
            Add(P + "RecentUncollected", "Unpaid admission (last day)", "未回収の入園料（直近1日）");
            Add(P + "RecentUncollected.Tip", "Fees visitors' households could not afford. Visitors still entered; a high value suggests the fee is too high for them.", "来園者の世帯が支払えなかった入園料です。来園自体は行われます。多い場合は入園料が高すぎる可能性があります。");
            Add(P + "LifetimeAdmission", "Admission income (total)", "入園料収入（累計）");
            Add(P + "ManagementCost", "Area management cost per day", "エリア管理費（1日あたり）");
            Add(P + "ManagementCost.Tip", "Grounds and administration overhead of the Park Area, paid from city money.", "公園エリアの管理・運営費で、市の資金から支払われます。");
            Add(P + "RecentExpense", "Area management paid (last day)", "エリア管理費支出（直近1日）");
            Add(P + "LifetimeExpense", "Area management paid (total)", "エリア管理費支出（累計）");
            Add(P + "RecentNet", "Net Area cash effect (last day)", "エリアの収支（直近1日）");
            Add(P + "RecentNet.Tip", "Admission income minus area management cost.", "入園料収入からエリア管理費を引いた値です。");
            Add(P + "Note.VanillaSeparate", "Normal upkeep and income of the buildings inside are handled by the base game and are not included here.", "エリア内の建物の通常の維持費と収入はゲーム本来の予算で扱われ、ここには含まれません。");
            Add(P + "Landscape", "Landscape score", "景観スコア");
            Add(P + "Landscape.Tip", "Vegetation, rest amenities, decoration, lighting and footpaths. Variety counts; repeating one object quickly stops helping.", "植栽・休憩設備・装飾・照明・歩道。種類の多さが重要で、同じ物の繰り返しはすぐに効果がなくなります。");
            Add(P + "Vegetation", "Vegetation", "植栽");
            Add(P + "Amenities", "Rest & amenities", "休憩・設備");
            Add(P + "Decoration", "Decoration", "装飾");
            Add(P + "Lighting", "Lighting", "照明");
            Add(P + "Paths", "Footpaths", "歩道");
            Add(P + "Diversity", "Category diversity", "カテゴリーの多様性");
            Add(P + "PathLength", "Footpath length", "歩道の長さ");
            Add(P + "TransitStops", "Nearby transit access points", "近くの公共交通アクセス地点");
            Add(P + "FacilityCategories", "Facility categories", "施設の種類");
            Add(P + "MaxLevel", "Highest level reached", "最高レベルに到達");
            Add(P + "NextLevel", "Next level", "次のレベル");
            Add(P + "NextQuality", "Quality needed", "必要な品質");
            Add(P + "Check.Experience", "Experience", "経験値");
            Add(P + "Check.Quality", "Park Quality", "公園品質");
            Add(P + "Check.Visitors", "Total visits", "累計来園数");
            Add(P + "Check.Variety", "Variety", "多様性");
            Add(P + "Check.Landscape", "Landscape", "景観");
            Add(P + "Check.Satisfaction", "Recent satisfaction", "直近の満足度");
            Add(P + "Check.Maintenance", "Maintenance", "維持管理");
            Add(P + "Check.PrestigeLandscape", "Landmark: landscape", "ランドマーク：景観");
            Add(P + "Check.PrestigeVariety", "Landmark: variety", "ランドマーク：多様性");
            Add(P + "Check.PrestigeTourists", "Landmark: tourist visits", "ランドマーク：観光客の来園数");
            Add(P + "Check.Hold", "Requirements held (about 1 day)", "条件の維持（約1日）");
            Add(P + "Note.Prestige", "Landmark Park needs any two of the three Landmark objectives.", "ランドマーク公園には3つのランドマーク目標のうち2つが必要です。");
            AppendPolicies(Add);
        }
        // Park policy texts (design §11). Directions only: magnitudes are added with each verified effect.
        private static void AppendPolicies(System.Action<string, string, string> Add)
        {
            void Policy(string id, string en, string jp, string enText, string jpText)
            { Add("Policy.TITLE[CS2AS.Policy." + id + "]", en, jp); Add("Policy.DESCRIPTION[CS2AS.Policy." + id + "]", enText, jpText); }
            Policy("EnhancedMaintenance", "Enhanced Maintenance", "維持管理の強化",
                "Spends more on Park management to raise effective maintenance coverage.", "公園の管理費を増やして実効的な維持管理の水準を高めます。");
            Policy("NightEvents", "Night Events", "ナイトイベント",
                "Draws more visitors with evening events, with more crowding and noise. Conflicts with Quiet Operations.",
                "夜間のイベントで来園者を増やしますが、混雑と騒音も増えます。静穏運営と同時には施行できません。");
            Policy("QuietOperations", "Quiet Operations", "静穏運営",
                "Lowers visitor pressure in favour of quiet, satisfying visits. Conflicts with Night Events.",
                "来園者の集中を抑え、静かで満足度の高い利用を優先します。ナイトイベントと同時には施行できません。");
            Policy("LandscapeProtection", "Landscape Protection", "景観保護",
                "Protects the landscape at the cost of some usable visitor capacity.", "利用できる来園者の収容力を一部犠牲にして景観を守ります。");
            Policy("PremiumServices", "Premium Services", "プレミアムサービス",
                "Better services soften the value-for-money penalty of higher admission fees.", "サービスを充実させ、高い入園料による割高感を和らげます。");
            Policy("MajorEventAttraction", "Major Event Attraction", "大型イベント誘致",
                "Attracts many more visitors with major events, with strong crowding and high upkeep.",
                "大型イベントで来園者を大きく増やしますが、混雑が大きく維持費も高くなります。");
        }
    }
}