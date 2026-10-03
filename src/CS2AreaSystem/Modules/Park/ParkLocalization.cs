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
            Add("CS2AS.Tab.Events", "Events", "イベント");
            Add(P + "Events.Tip", "Park events are paid from city money as Area Management and change only this Park: during a Park Festival 15% of the comfortable visitor capacity is used by the festival, known visit satisfaction gets +5 and visit XP ×1.25. Vanilla attractiveness and where citizens go are not changed.",
                "パークイベントは市の資金からエリア管理費として支払われ、このパークの中だけに作用します。パークフェスティバル中は快適に受け入れられる来園者数の15%をフェスティバルが使い、記録された来園満足度に+5、来園XPが1.25倍になります。バニラの魅力度や市民の行き先は変わりません。");
            Add(P + "Events.Unavailable", "Park events are unavailable (Area finance or Park gameplay failed to load). No event can be started and no event effect applies.",
                "パークイベントは利用できません（エリア財政またはパーク機能の読み込みに失敗しました）。イベントは開始できず、効果も適用されません。");
            Add(P + "Events.Status", "Event", "イベント");
            Add(P + "Events.Status.None", "None", "なし");
            Add(P + "Events.Status.Pending", "Waiting for city money", "市の資金からの支出待ち");
            Add(P + "Events.Status.Active", "Park Festival running", "パークフェスティバル開催中");
            Add(P + "Events.Status.Paused", "Park Festival paused (Park suspended)", "パークフェスティバル一時停止中（パーク停止中）");
            Add(P + "Events.RemainingHours", "Hours remaining", "残り時間（時間）");
            Add(P + "Events.Effects", "Festival effects: comfortable capacity −15%, visit satisfaction +5, visit XP ×1.25.",
                "フェスティバル効果：快適収容数 −15%、来園満足度 +5、来園XP ×1.25。");
            Add(P + "Events.Cost", "Festival cost", "フェスティバル費用");
            Add(P + "Events.Cost.Tip", "Half of this Park's current daily management cost, charged once when the festival is started. It starts only after the city has paid; if nothing can be paid it does not start and nothing is charged.",
                "このパークの現在の1日あたり管理費の半額で、開始時に1回だけ請求されます。市が支払った後にだけ開始し、支払えなかった場合は開始せず請求もされません。");
            Add(P + "Events.PendingCost", "Requested cost", "請求額");
            Add(P + "Events.Start.Tip", "Starts a Park Festival for one full in-game day once the city has paid. It does not repeat automatically.",
                "市の支払い後、ゲーム内でまる1日パークフェスティバルを開催します。自動では繰り返しません。");
            Add(P + "Events.Name.1", "Park Festival", "パークフェスティバル");
            Add(P + "Events.Start.1", "Start Park Festival", "パークフェスティバルを開始");
            Add(P + "Events.Blocked.Unavailable", "Park events are not ready yet (loading or unavailable).", "パークイベントはまだ利用できません（読み込み中または利用不可）。");
            Add(P + "Events.Blocked.NoState", "This Park has no event data yet.", "このパークにはまだイベントデータがありません。");
            Add(P + "Events.Blocked.Inactive", "Only an active Park can start an event.", "イベントを開始できるのは稼働中のパークだけです。");
            Add(P + "Events.Blocked.UnknownEvent", "Unknown event.", "不明なイベントです。");
            Add(P + "Events.Blocked.Pending", "The festival payment is still being processed.", "フェスティバルの支払いを処理中です。");
            Add(P + "Events.Blocked.Active", "A festival is already running in this Park.", "このパークではすでにフェスティバルを開催中です。");
            Add(P + "Events.Blocked.NoCost", "The management cost is still being calculated.", "管理費を計算中です。");
            Add(P + "Events.LifetimeEvents", "Festivals held", "開催したフェスティバル");
            Add(P + "Events.LifetimeExpense", "Total event cost", "イベント費用の累計");
            Add(P + "Events.LifetimeExpense.Tip", "Only the amounts actually paid from city money, including a payment for a festival that could not start because the Park stopped being active.",
                "実際に市の資金から支払われた額だけです。パークが稼働中でなくなり開始できなかったフェスティバルの支払いも含みます。");
            Add(P + "Events.History", "Recent festivals (newest first)", "最近のフェスティバル（新しい順）");
            Add(P + "Events.History.Tip", "Amount paid by the city for this festival.", "このフェスティバルに市が支払った額です。");
            Add("CS2AS.Tab.Progress", "Progress", "進捗");
            Add(P + "Summary.BothRequired", "Suspended: assign a Park Maintenance headquarters and connect a dedicated pedestrian path across the boundary.", "停止中：公園整備施設を本部に指定し、専用歩道で境界の内外を接続してください。");
            Add(P + "Summary.HeadquartersRequired", "Suspended: assign a Park Maintenance building inside this area as headquarters.", "停止中：エリア内の公園整備施設を本部に指定してください。");
            Add(P + "Summary.HeadquartersInvalid", "Suspended: the saved headquarters is invalid. Assign a Park Maintenance building; progress is preserved.", "停止中：保存された本部は無効です。公園整備施設を指定してください。進行は保持されます。");
            Add(P + "Summary.EntrancePending", "Headquarters ready; waiting for a dedicated-path entrance scan.", "本部は有効です。専用歩道の入口を調査中です。");
            Add(P + "Warning.HeadquartersRequired", "Park Maintenance headquarters required; Level, XP and finance are kept.", "公園整備施設の本部が必要です。レベル・経験値・会計は保持されます。");
            Add(P + "Warning.HeadquartersInvalid", "Saved headquarters is invalid; assign Park Maintenance inside this area.", "保存された本部が無効です。エリア内の公園整備施設を指定してください。");
            Add(P + "Summary.Active", "Open. Visits are measured from real residents' and tourists' leisure trips.", "開園中。来園者は住民と観光客の実際の余暇行動から計測されます。");
            Add(P + "Summary.EntranceRequired", "Connect a dedicated pedestrian path from outside into the Park, or assign an explicitly registered entrance asset.", "専用歩道で公園の内外を接続するか、登録済みの入口アセットを指定してください。");
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
            Add(P + "Diagnostic.LowVegetation", "Little vegetation for this Park's size.", "公園の広さに対して植栽が少なめです。");
            Add(P + "Diagnostic.LowVegetation.Tip", "Vegetation scores below half its weight. Trees and plants of different kinds help most.", "植栽の評価が配点の半分未満です。種類の異なる樹木や植物が最も効果的です。");
            Add(P + "Diagnostic.LowAmenities", "Few rest amenities such as benches or tables.", "ベンチやテーブルなどの休憩設備が少なめです。");
            Add(P + "Diagnostic.LowAmenities.Tip", "Rest & amenities score below half its weight.", "休憩・設備の評価が配点の半分未満です。");
            Add(P + "Diagnostic.LowDecoration", "Little decoration from the Park or stone prop menus.", "公園・石材の小物による装飾が少なめです。");
            Add(P + "Diagnostic.LowDecoration.Tip", "Decoration scores below half its weight.", "装飾の評価が配点の半分未満です。");
            Add(P + "Diagnostic.LowLighting", "Little lighting inside the Park.", "園内の照明が少なめです。");
            Add(P + "Diagnostic.LowLighting.Tip", "Lighting scores below half its weight.", "照明の評価が配点の半分未満です。");
            Add(P + "Diagnostic.LowPaths", "Few footpaths for this Park's size.", "公園の広さに対して歩道が少なめです。");
            Add(P + "Diagnostic.LowPaths.Tip", "Footpaths score below half their weight.", "歩道の評価が配点の半分未満です。");
            Add(P + "Diagnostic.SparseAmenities", "Rest amenities are concentrated in a small part of the Park.", "休憩設備が公園の一部に偏っています。");
            Add(P + "Diagnostic.SparseAmenities.Tip", "Fewer than a quarter of the Park's landscaped sections have amenities. The button shows a busy section without any. Advisory only: the score is unchanged.", "景観要素のある区画のうち、休憩設備がある区画が4分の1未満です。ボタンで設備のない区画の一つを表示します。助言のみでスコアは変わりません。");
            Add(P + "Diagnostic.SparseLighting", "Lighting is concentrated in a small part of the Park.", "照明が公園の一部に偏っています。");
            Add(P + "Diagnostic.SparseLighting.Tip", "Fewer than a quarter of the Park's landscaped sections have lighting. The button shows a busy section without any. Advisory only: the score is unchanged.", "景観要素のある区画のうち、照明がある区画が4分の1未満です。ボタンで照明のない区画の一つを表示します。助言のみでスコアは変わりません。");
            Add(P + "FocusTarget", "Show location", "場所を表示");
            Add(P+"Preview.Budget","Preview: candidate budget","試算：予算候補");
            Add(P+"Preview.Fee","Preview: candidate fee","試算：入場料候補");
            Add(P+"Preview.Cost","Estimate: daily management cost","試算：1日の管理費");
            Add(P+"Preview.Coverage","Estimate: management coverage","試算：管理充足率");
            Add(P+"Preview.Maintenance","Estimate: maintenance score","試算：管理評価");
            Add(P+"Preview.Value","Estimate: value for money","試算：料金満足度");
            Add(P+"Preview.Net","Estimate: daily net","試算：1日の収支");
            Add(P+"Preview.Tip","Estimate using current Quality and recent visitor volume, assuming full fee collection. Demand, household affordability and routing are not predicted. Preview controls do not apply settings.","現在のQualityと直近の来訪数を固定し、全額回収を仮定した試算です。需要・家計の支払能力・経路は予測しません。試算操作は設定を適用しません。");
            Add(P+"Connectivity.Status","Pedestrian connectivity (advisory)","歩行接続診断（助言）");
            Add(P+"Connectivity.Unknown","Unknown / waiting for entrance evidence","不明・入口情報の待機中");
            Add(P+"Connectivity.Partial","Partial scan — connectivity not yet determined","部分スキャン・接続は未確定");
            Add(P+"Connectivity.Complete","Complete scan","スキャン完了");
            Add(P+"Connectivity.Reachable","Facilities reached from entrances","入口から接続する施設");
            Add(P+"Connectivity.Unattached","Facilities with unknown network attachment","歩行ネットワーク接続先が不明な施設");
            Add(P+"Connectivity.Disconnected","Facility not connected to the entrance pedestrian network","入口の歩行ネットワークに接続していない施設");
            Add(P + "FocusTarget.Tip", "Moves the camera to a representative object for this finding. Nothing in the city is changed.", "この診断の代表的なオブジェクトにカメラを移動します。都市の状態は何も変更しません。");
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
        // Park policy texts (design §11). Implemented effects state their ParkPolicyEffects magnitudes; the unavailable
        // ones keep their direction only.
        private static void AppendPolicies(System.Action<string, string, string> Add)
        {
            void Policy(string id, string en, string jp, string enText, string jpText)
            { Add("Policy.TITLE[CS2AS.Policy." + id + "]", en, jp); Add("Policy.DESCRIPTION[CS2AS.Policy." + id + "]", enText, jpText); }
            Policy("EnhancedMaintenance", "Enhanced Maintenance", "維持管理の強化",
                "Park management is funded 25% above the budget setting: the management expense and the maintenance coverage both rise by 25%.",
                "公園の管理費を予算設定より25%多く支出します。管理費と維持管理のカバー率がともに25%上がります。");
            Policy("NightEvents", "Night Events", "ナイトイベント",
                "Draws more visitors with evening events, with more crowding and noise. Conflicts with Quiet Operations.",
                "夜間のイベントで来園者を増やしますが、混雑と騒音も増えます。静穏運営と同時には施行できません。");
            Policy("QuietOperations", "Quiet Operations", "静穏運営",
                "Lowers visitor pressure in favour of quiet, satisfying visits. Conflicts with Night Events.",
                "来園者の集中を抑え、静かで満足度の高い利用を優先します。ナイトイベントと同時には施行できません。");
            Policy("LandscapeProtection", "Landscape Protection", "景観保護",
                "Vegetation and decoration count 25% more toward the Landscape score; 15% of the comfortable visitor capacity is closed to visitors.",
                "植栽と装飾が景観スコアに25%多く寄与します。快適に受け入れられる来園者数は15%減ります。");
            Policy("PremiumServices", "Premium Services", "プレミアムサービス",
                "Visitors consider an admission fee up to 30% higher to be fair, which softens the value-for-money penalty of higher fees.",
                "来園者が妥当と感じる入園料が30%上がり、高い入園料による割高感を和らげます。");
            Policy("MajorEventAttraction", "Major Event Attraction", "大型イベント誘致",
                "Attracts many more visitors with major events, with strong crowding and high upkeep.",
                "大型イベントで来園者を大きく増やしますが、混雑が大きく維持費も高くなります。");
        }
    }
}
