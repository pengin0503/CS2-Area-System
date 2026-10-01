using System.Collections.Generic;
namespace CS2AreaSystem.Modules.Industrial
{
    public static class IndustrialLocalization
    {
        public static void Append(Dictionary<string, string> e, bool ja)
        {
            void Add(string key, string en, string jp) => e[key] = ja ? jp : en;
            const string P = IndustrialPanelBuilder.P;
            Add("CS2AS.Tab.Production", "Production", "生産");
            Add("CS2AS.Tab.Logistics", "Logistics", "物流");
            Add("CS2AS.Tab.Employment", "Employment", "雇用");
            Add("CS2AS.Tab.Progress", "Progress", "進捗");
            Add(P + "Summary.Active", "Active. Metrics come from the real companies inside this area, observed once per game hour.", "稼働中。指標はエリア内の実際の企業を1ゲーム時間ごとに観測した値です。");
            Add(P + "Summary.HeadquartersRequired", "Assign an extractor, factory, warehouse or cargo building inside this area as the headquarters to activate it.", "エリア内の採取施設・工場・倉庫・貨物施設の建物を本部に割り当てると稼働します。");
            Add(P + "Summary.HeadquartersInvalid", "Suspended: the headquarters building was removed or is no longer inside this area. Assign a new headquarters; Level and XP are kept.", "停止中：本部の建物が撤去されたか、エリア外になりました。新しい本部を割り当ててください。レベルと経験値は保持されます。");
            Add(P + "LevelName.1", "Local Industry", "地域産業");
            Add(P + "LevelName.2", "Industrial District", "産業地区");
            Add(P + "LevelName.3", "Production Hub", "生産拠点");
            Add(P + "LevelName.4", "Regional Industrial Center", "広域産業センター");
            Add(P + "LevelName.5", "Industrial Powerhouse", "産業大拠点");
            Add(P + "Level", "Industrial level", "産業レベル");
            Add(P + "Experience", "Experience", "経験値");
            Add(P + "Headquarters", "Headquarters", "本部");
            Add(P + "Hq.Valid", "Assigned", "割り当て済み");
            Add(P + "Hq.Invalid", "Removed or outside the area", "撤去済み・エリア外");
            Add(P + "Hq.Missing", "Not assigned", "未割り当て");
            Add(P + "Calculating", "Waiting for the first hourly observation…", "最初の1時間ごとの観測を待っています…");
            Add(P + "PartialData", "Partial data (observed / eligible)", "一部のデータ（観測数 / 対象数）");
            Add(P + "ObservedHours", "Observed game hours", "観測済みゲーム時間");
            Add(P + "Status.Unavailable", "Unavailable (not counted)", "利用不可（計算に含めません）");
            Add(P + "Status.NotApplicable", "Not applicable", "該当なし");
            Add(P + "Status.WarmingUp", "Warming up (needs more observed hours)", "観測中（観測時間が不足）");
            Add(P + "Status.Stale", "Paused or delayed: last value, not current", "一時停止・遅延：前回の値で現在値ではありません");
            // Metrics.
            Add(P + "Metric.Performance", "Industrial Performance", "産業パフォーマンス");
            Add(P + "Metric.Performance.Tip", "Production 35%, Logistics 25%, Employment 20%, Efficiency 20%. One missing component is reweighted; with two missing it is unavailable.", "生産35%・物流25%・雇用20%・効率20%。1つ欠けた場合は残りで再配分し、2つ以上欠けた場合は算出しません。");
            Add(P + "Metric.Production", "Production", "生産");
            Add(P + "Metric.Production.Tip", "Productive activity 65%, active producers 25%, output diversity 10%. Requires a validated activity reading.", "生産稼働率65%・稼働中の生産施設25%・生産品目の多様性10%。検証済みの稼働率が必要です。");
            Add(P + "Metric.Logistics", "Logistics", "物流");
            Add(P + "Metric.Logistics.Tip", "Input continuity 50% and output/dispatch continuity 50% over the last six game hours. Warehouses or terminals give no points by themselves.", "直近6ゲーム時間の投入継続性50%・出荷継続性50%。倉庫や貨物ターミナルがあるだけでは加点されません。");
            Add(P + "Metric.Employment", "Employment", "雇用");
            Add(P + "Metric.Employment.Tip", "Filled industrial jobs 75% and per-facility staffing balance 25%. Office-only jobs are excluded.", "産業の充足率75%・施設ごとの人員バランス25%。オフィスのみの雇用は含みません。");
            Add(P + "Metric.Efficiency", "Efficiency", "効率");
            Add(P + "Metric.Efficiency.Tip", "Vanilla building efficiency 70% and company profitability 30%.", "ゲーム本来の建物効率70%・企業の収益性30%。");
            Add(P + "Metric.Activity", "Productive activity (sampled)", "生産稼働率（サンプリング）");
            Add(P + "Metric.Activity.Tip", "Current production rate shown by the game for each company, divided by its rate at full staff and 100% efficiency. Job-weighted.", "ゲームが各企業に表示する現在の生産量を、満員・効率100%時の生産量で割った値（雇用数で加重）。");
            Add(P + "Metric.ActiveShare", "Active producers", "稼働中の生産施設");
            Add(P + "Metric.ActiveShare.Tip", "Extractor and processing buildings currently producing, out of all of them (vacant ones included).", "採取・加工施設のうち、現在生産しているものの割合（空き施設を含む）。");
            Add(P + "Metric.Diversity", "Output diversity", "生産品目の多様性");
            Add(P + "Metric.Diversity.Tip", "1, 2 or 3+ outputs with at least 20% of the leading output's activity score 50, 75 or 100.", "主要品目の20%以上の稼働がある品目が1・2・3以上で、それぞれ50・75・100。");
            Add(P + "Metric.InputContinuity", "Input continuity", "投入の継続性");
            Add(P + "Metric.InputContinuity.Tip", "Share of observed time producers could still make at least one unit from their stock of inputs.", "生産施設が在庫の原料で少なくとも1単位生産できた観測時間の割合。");
            Add(P + "Metric.OutputContinuity", "Output/dispatch continuity", "出荷の継続性");
            Add(P + "Metric.OutputContinuity.Tip", "Share of observed time producers were not blocked by full storage.", "生産施設が満杯の保管で止まっていなかった観測時間の割合。");
            Add(P + "Metric.JobFill", "Job fill", "求人充足率");
            Add(P + "Metric.JobFill.Tip", "Employees / jobs of industrial companies and logistics facilities in this area.", "エリア内の産業企業と物流施設の従業員数 / 雇用枠。");
            Add(P + "Metric.StaffingBalance", "Staffing balance", "人員バランス");
            Add(P + "Metric.StaffingBalance.Tip", "Average fill of the facilities that offer jobs.", "雇用枠のある施設ごとの充足率の平均。");
            Add(P + "Metric.BuildingEfficiency", "Building efficiency", "建物効率");
            Add(P + "Metric.BuildingEfficiency.Tip", "Vanilla efficiency (100% = normal; bonuses can exceed 100%).", "ゲーム本来の効率（100%が標準。ボーナスで100%を超えることがあります）。");
            Add(P + "Metric.Profitability", "Profitability", "収益性");
            Add(P + "Metric.Profitability.Tip", "Vanilla company profitability mapped from Bankrupt (0–20) to Profitable (80–100).", "ゲーム本来の企業収益性を、倒産寸前(0–20)から黒字(80–100)に換算。");
            // Overview rows and warnings.
            Add(P + "ActiveFacilities", "Producing facilities", "生産中の施設");
            Add(P + "EmployeesJobs", "Employees / jobs", "従業員 / 雇用枠");
            Add(P + "Bottlenecks.None", "No bottlenecks observed", "ボトルネックは観測されていません");
            Add(P + "OverlapMembers", "Overlapping buildings counted by another Industrial Area", "他の産業エリアで集計される重複建物");
            Add(P + "LastUpdate", "Last observation (game day, time)", "最終観測（ゲーム日・時刻）");
            Add(P + "LastUpdate.Tip", "Start of the last completed hourly observation.", "直近に完了した1時間ごとの観測の開始時刻。");
            Add(P + "Warning.HeadquartersRequired", "No headquarters: the area is suspended and earns no XP.", "本部がありません。エリアは停止中で経験値は得られません。");
            Add(P + "Warning.HeadquartersInvalid", "The headquarters building is gone or outside the area: suspended.", "本部の建物がないか、エリア外です。停止中です。");
            Add(P + "Warning.Paused", "Suspended: the values below are the last observation and are paused.", "停止中：以下は前回の観測値で、更新は停止しています。");
            Add(P + "Warning.Delayed", "The last observation took longer than one game hour; it counts for no XP or coverage.", "前回の観測が1ゲーム時間を超えたため、経験値や観測時間には含まれません。");
            Add(P + "Warning.Observing", "Observing real companies. Scores appear after the first hourly observation.", "実際の企業を観測中です。最初の1時間ごとの観測後に表示されます。");
            Add(P + "Warning.ReadFailures", "Some buildings could not be read and were skipped:", "読み取れず除外した建物があります：");
            // Bottlenecks and suggestions.
            Add(P + "Bottleneck.WorkforceShortage", "Workforce shortage", "人手不足");
            Add(P + "Bottleneck.WorkforceShortage.Tip", "Job fill below 75% (fill · understaffed facilities · observed hours).", "充足率75%未満（充足率・人手不足の施設数・観測時間）。");
            Add(P + "Suggestion.WorkforceShortage", "Improve housing, education or transit access for workers.", "労働者の住宅・教育・交通アクセスを改善しましょう。");
            Add(P + "Bottleneck.LowEfficiency", "Low building efficiency", "建物効率の低下");
            Add(P + "Bottleneck.LowEfficiency.Tip", "Building efficiency below 70% (efficiency · facilities · observed hours).", "建物効率70%未満（効率・施設数・観測時間）。");
            Add(P + "Suggestion.LowEfficiency", "Check the buildings' efficiency details: electricity, water, garbage, mail or staffing.", "建物の効率内訳（電気・水道・ごみ・郵便・人員）を確認しましょう。");
            Add(P + "Bottleneck.LowProfitability", "Low profitability", "低い収益性");
            Add(P + "Bottleneck.LowProfitability.Tip", "Companies losing money or worse (viability · companies' facilities · observed hours).", "赤字以下の企業（収益性・施設数・観測時間）。");
            Add(P + "Suggestion.LowProfitability", "Review input costs, taxes and demand for the products made here.", "原料コスト・税率・この地域の製品の需要を見直しましょう。");
            Add(P + "Bottleneck.InputStarvation", "Input starvation", "原料不足");
            Add(P + "Bottleneck.InputStarvation.Tip", "Producers that had workers but not enough input stock for one unit (continuity · facilities · observed hours).", "人員はいるのに原料在庫が1単位分に満たない生産施設（継続性・施設数・観測時間）。");
            Add(P + "Suggestion.InputStarvation", "Bring suppliers of the missing inputs closer or improve freight access to this area.", "不足している原料の供給元を近くに置くか、貨物の交通アクセスを改善しましょう。");
            Add(P + "Bottleneck.StorageSaturation", "Output/storage saturation", "出荷・保管の飽和");
            Add(P + "Bottleneck.StorageSaturation.Tip", "Producers whose storage was full (continuity · facilities · observed hours). The cause is not assumed.", "保管が満杯だった生産施設（継続性・施設数・観測時間）。原因は推定しません。");
            Add(P + "Suggestion.StorageSaturation", "Check demand for the outputs and freight access for outgoing trucks, trains or ships.", "生産品の需要と、出荷するトラック・列車・船の交通アクセスを確認しましょう。");
            Add(P + "Bottleneck.InsufficientCargo", "Insufficient cargo support", "貨物支援の不足");
            Add(P + "Bottleneck.InsufficientCargo.Tip", "Output is blocked while cargo facilities in this area are not operating (continuity · idle cargo facilities · observed hours).", "出荷が滞る一方で、エリア内の貨物施設が稼働していません（継続性・停止中の貨物施設数・観測時間）。");
            Add(P + "Suggestion.InsufficientCargo", "Restore the area's cargo stations (staff, utilities, budget) or connect it to another terminal.", "エリアの貨物駅を復旧する（人員・公共サービス・予算）か、別のターミナルに接続しましょう。");
            Add(P + "Bottleneck.LowActiveProducerShare", "Few active producers", "稼働中の生産施設が少ない");
            Add(P + "Bottleneck.LowActiveProducerShare.Tip", "Producing facilities below 75% of all extractor/processing buildings (share · idle or vacant facilities · observed hours).", "生産中の施設が採取・加工施設全体の75%未満（割合・停止または空きの施設数・観測時間）。");
            Add(P + "Suggestion.LowActiveProducerShare", "Look at idle or vacant buildings: demand, workers and inputs decide whether companies move in and produce.", "停止中・空きの建物を確認しましょう。需要・労働者・原料が企業の入居と生産を左右します。");
            // Production tab.
            Add(P + "Extractors", "Extractors", "採取施設");
            Add(P + "Processors", "Processors / manufacturers", "加工・製造施設");
            Add(P + "Producers", "Producing / all producers", "生産中 / 全生産施設");
            Add(P + "RawActivity", "Activity incl. bonuses", "稼働率（ボーナス込み）");
            Add(P + "RawActivity.Tip", "Unclamped value; only scoring is limited to 100%.", "上限なしの値です。スコア計算のみ100%で頭打ちにします。");
            Add(P + "MeaningfulOutputs", "Meaningful outputs (now / six-hour average)", "有意な生産品目（現在 / 6時間平均）");
            Add(P + "Resource.Tip", "Current production rate per day shown by the game for companies here (sampled, not a lifetime counter).", "エリア内の企業についてゲームが表示する1日あたりの現在の生産量（サンプリング値で累計ではありません）。");
            // Logistics tab.
            Add(P + "StorageFacilities", "Storage facilities", "保管施設");
            Add(P + "StorageUtilization", "Stored / capacity", "保管量 / 容量");
            Add(P + "CargoFacilities", "Cargo facilities (operating / all)", "貨物施設（稼働 / 全体）");
            // Employment tab.
            Add(P + "Jobs", "Jobs", "雇用枠");
            Add(P + "Employees", "Employees", "従業員");
            Add(P + "Vacancies", "Vacancies", "欠員");
            Add(P + "ProducerJobs", "Producers: employees / jobs", "生産施設：従業員 / 雇用枠");
            Add(P + "SupportJobs", "Logistics & support: employees / jobs", "物流・支援：従業員 / 雇用枠");
            Add(P + "Vacancy.0", "Vacant: uneducated jobs", "欠員：教育不要の職");
            Add(P + "Vacancy.1", "Vacant: poorly educated jobs", "欠員：初等教育の職");
            Add(P + "Vacancy.2", "Vacant: educated jobs", "欠員：中等教育の職");
            Add(P + "Vacancy.3", "Vacant: well educated jobs", "欠員：高等教育の職");
            Add(P + "Vacancy.4", "Vacant: highly educated jobs", "欠員：大学院教育の職");
            // Progress tab.
            Add(P + "NextLevel", "Next level", "次のレベル");
            Add(P + "MaxLevel", "Highest level reached", "最高レベルに到達");
            Add(P + "Check.Experience", "Experience", "経験値");
            Add(P + "Check.Performance", "Industrial Performance", "産業パフォーマンス");
            Add(P + "Check.ActiveFacilities", "Producing facilities", "生産中の施設");
            Add(P + "Check.Employees", "Employed workers", "従業員数");
            Add(P + "Check.Logistics", "Logistics", "物流");
            Add(P + "Check.Production", "Production", "生産");
            Add(P + "Check.LogisticsRoute", "Developed logistics route (operating cargo facility or Logistics ≥ 70)", "物流ルートの整備（稼働中の貨物施設、または物流70以上）");
            Add(P + "Check.PrestigeLogistics", "Prestige: Logistics ≥ 80", "名声目標：物流80以上");
            Add(P + "Check.PrestigeEmployment", "Prestige: Employment ≥ 85", "名声目標：雇用85以上");
            Add(P + "Check.PrestigeEfficiency", "Prestige: Efficiency ≥ 80", "名声目標：効率80以上");
            Add(P + "Check.PrestigeOutputs", "Prestige: 3+ meaningful outputs", "名声目標：有意な生産品目3つ以上");
            Add(P + "Check.PrestigeStreak", "Prestige: 7 consecutive high-activity days", "名声目標：高稼働日7日連続");
            Add(P + "HourlyXp", "XP this hour", "この1時間の経験値");
            Add(P + "HourlyXp.Tip", "Production 35%, Employment 25%, Logistics 20%, Performance 20% (three of four needed), divided by 10, at most 10 per game hour. No bonus for area size.", "生産35%・雇用25%・物流20%・パフォーマンス20%（4つ中3つ必要）を10で割った値で、1ゲーム時間あたり最大10。エリアの広さによる加算はありません。");
            Add(P + "DailyXp", "XP today", "本日の経験値");
            Add(P + "DailyXp.Tip", "At most 240 XP per game day.", "1ゲーム日あたり最大240。");
            Add(P + "XpBlocked.Suspended", "No XP: the area is suspended.", "経験値なし：エリアは停止中です。");
            Add(P + "XpBlocked.Stale", "No XP: the observation was delayed or not current.", "経験値なし：観測が遅延したか最新ではありません。");
            Add(P + "XpBlocked.NoOperatingProducer", "No XP: no facility is producing.", "経験値なし：生産中の施設がありません。");
            Add(P + "XpBlocked.InsufficientInputs", "No XP: fewer than three of Production, Employment, Logistics and Performance are available.", "経験値なし：生産・雇用・物流・パフォーマンスのうち利用できるものが3つ未満です。");
            Add(P + "XpBlocked.DailyCap", "Daily XP cap of 240 reached.", "1日の上限240に達しました。");
            Add(P + "XpBlocked.AlreadySettled", "This game hour was already settled.", "このゲーム時間は精算済みです。");
            Add(P + "StreakDays", "High-activity day streak", "高稼働日の連続日数");
            Add(P + "DayAverage", "Today's activity average (time-weighted)", "本日の稼働率平均（時間加重）");
            Add(P + "DayHours", "Today's valid observed hours", "本日の有効観測時間");
            Add(P + "LastDay", "Last completed day", "前日の判定");
            Add(P + "LastDay.Tip", "A day counts with at least 18 valid observed game hours and a time-weighted activity average of at least 80.", "有効観測時間18時間以上かつ時間加重の稼働率平均80以上の日が該当します。");
            Add(P + "Day.Qualified", "Qualified", "達成");
            Add(P + "Day.InsufficientCoverage", "Not counted: fewer than 18 observed hours", "対象外：観測時間が18時間未満");
            Add(P + "Day.LowAverage", "Not counted: average below 80", "対象外：平均が80未満");
            // Notes.
            Add(P + "Note.Sampled", "Production figures are sampled current rates of the real companies, not lifetime production counters.", "生産の数値は実際の企業の現在の生産量をサンプリングした値で、累計生産量ではありません。");
            Add(P + "Note.NoPlacementBonus", "Placing warehouses or cargo terminals gives no points by itself; only observed flow counts.", "倉庫や貨物ターミナルを置くだけでは加点されません。観測された物流のみが評価されます。");
            Add(P + "Note.ExternalCargo", "Cargo routes outside this area are not observed; a missing terminal here is not treated as a problem.", "エリア外の貨物ルートは観測しません。エリア内にターミナルがないこと自体は問題として扱いません。");
            Add(P + "Note.OfficesExcluded", "Office-only workplaces are not counted as industrial jobs.", "オフィスのみの職場は産業の雇用に含めません。");
            Add(P + "Note.NoBuff", "Level does not directly increase production, cargo capacity, efficiency, profitability, taxes or demand.", "レベルは生産・貨物能力・効率・収益性・税収・需要を直接増やしません。");
            Add(P + "Note.Prestige", "Industrial Powerhouse needs any two prestige objectives.", "産業大拠点には名声目標のうち任意の2つが必要です。");
            Add(P + "Note.HqHealth", "The headquarters' vacancy or poor operating health never suspends the area and has no penalty in this version.", "本部の空きや経営不振でエリアが停止することはなく、このバージョンでは不利な効果もありません。");
            Add(P + "Note.Overlap", "These buildings belong to an overlapping Industrial Area with a smaller ID, which counts them.", "これらの建物はIDの小さい重複した産業エリアに属し、そちらで集計されます。");
        }
    }
}
