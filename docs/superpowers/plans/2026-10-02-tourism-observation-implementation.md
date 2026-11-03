# Tourism Area v1 — 観測・成長・UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 実際の観光・商業利用を重複なく観測し、評価、永久レベル、履歴、運営と商業の状態を既存パネルで表示する。

**Architecture:** 施設DTO、共有する実利用観測、純粋なセッション・評価・XPモデルを分離する。Coreに共有観測feedを置く場合もPark/Tourism実装へ依存させない。保存するのは確定した統計と報酬境界であり、全市民の派生リストは保存しない。

**Tech Stack:** C# 9 / Unity ECS・Jobs / .NET Framework 4.8製品ビルド・net8 API-check / NUnit 4 / React・TypeScript。

**Spec:** `docs/superpowers/specs/2026-10-02-tourism-area-v1-design.md`。ユーザーは2026-10-02に書面仕様を承認した。mainの調査基点は`b2366de971cb2aa206f1b970aaaace0f5dd6c4f0`。実行開始時には再取得し、差分があれば対象ファイルを再確認する。

## Global Constraints

- 対象: Cities: Skylines II 1.6.2f1。API-checkの出力はプレイ用MODではない。
- 建物の外観は問わない。許可業種はLodging／Meals／Entertainment／Recreation。一般商業を黙って許可するfallbackは禁止。
- 本部・入口は不要。Area Core、公園、産業、元の都市全体のprefab・需要・観光処理を壊さない。
- 運営はOFF／標準／強化、初期OFF、レベル1から。プロモーションと回遊の相対重み1／1.15／1.30、宿泊は1／1.10／1.20。同一判断で重複乗算しない。
- XPは1時間上限10、1ゲーム日上限240、観測カバレッジ75%以上、昇格は直近24時間中18時間以上の有効観測。レベルは降格しない。
- Game DLL／Toolchain／UI媒体をリポジトリへ含めない。外部コードをコピーするならライセンス確認とTHIRD_PARTY.mdへの記録を先行する。
- 実行時の可用性、保存互換性、行動効果は単なるフィールド存在や合成テストで証明しない。未成立経路では該当後続作業を止め、理由と代替案を記録する。
- 承認済みの旧Core／Park／Industrial仕様・計画を改変しない。観光拡張に必要な旧Tourism境界テストのみ、意図を保って更新する。
- この計画の作成時には製品コード・ゲーム内検証を実施していない。チェック欄は実装担当者が証拠を得てから更新する。

## Review Focus

- 会社Entityと建物Entity、宿泊予約と滞在の差を誤って訪問へ変換しない（A1/A2: CompanyPropertyMappingDoesNotInventVisit）。
- 更新間隔・pause・途中ロードで同じ利用と時間報酬を再加算しない（A2/A4: SeededVisitCompletionIsNotCounted、ReloadDoesNotRepeatHourXp）。
- ゲーム日の境界と端数時間を整数の262144/24で誤計算しない（A1/A4: ClockAgreesAtMidnightAndHourBoundary）。
- データ欠測・ホテルなし・実際のゼロを混同しない（A3: MissingActivityBlocksXpButMissingLodgingDoesNotLowerScore）。
- 共有観測の失敗が既存Parkの入園料・訪問・ロード安全性を変えない（A2/A6: SharedFeedPreservesParkVisitAndAdmissionContracts）。

---

## ファイル構成

観光専用のmodel・保存・panelは`src/CS2AreaSystem/Modules/Tourism/`。共有feedはCoreに置くが、Parkの型を参照しない。CとOのスナップショットを受け取ってUIで合成する。

### Task A1: 観測経路、DTO、時計を固定する

**Files:** Create:
- `src/CS2AreaSystem/Modules/Tourism/TourismContracts.cs`
- `src/CS2AreaSystem/Modules/Tourism/TourismFacilityClassifier.cs`
- `docs/verification/tourism-observation-contracts.md`
- `tests/CS2AreaSystem.Tests/TourismFacilityClassifierTests.cs`
- `tests/CS2AreaSystem.Tests/TourismObservationApiContractTests.cs`

**Interfaces:** Produces:
- flags `TourismFacilityCategory : byte`（Lodging=1、Meals=2、Entertainment=4、Recreation=8、Park=16、Attraction=32）。
- `TourismMetric`（Status enum: Unavailable/Available/Partial/WarmingUp/Stale、Value/Observed/Eligible double、HasValue bool）。非有限値はUnavailable。
- `TourismFacilityFacts`（Entity Building、Company、Resource AllowedSold、TourismFacilityCategory Categories、bool Operating、RoadOrPathConnected、double? VanillaAttractiveness）。
- `TourismFacilitySnapshot`（ulong AreaId、int Generation、EligibleFacilities、OperatingHotels、int CategoryCount、TourismMetric Attractiveness、RoadPathCoverage、PublicTransportCoverage）。
- `TourismClock`（double Hours、long HourBucket、uint Day、uint Frame）。既存のIndustrialGameClock.From(TimeData)の意味を確認し、同等の検証済み時刻変換を観光側へ実装する。観光からIndustrialのゲームプレイへ依存させない。262144/24の整数商を1時間の長さにせず、hour/day境界をTimeDataで検証する。
- `TourismFacilityCategory TourismFacilityClassifier.Classify(in TourismFacilityFacts facts)`。

- [ ] Public ECSとprefabを追い、会社→物件、Citizen→Household→TouristHousehold、実利用、ホテル予約・空室・定員、歩行接続、稼働路線を文書化する。
- [ ] `CompanyPropertyMappingDoesNotInventVisit`、`ClockAgreesAtMidnightAndHourBoundary`と4Resource分類の失敗するテストを書く。
- [ ] `dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug -p:AreaApiCheck=true --filter "FullyQualifiedName~TourismFacilityClassifierTests|FullyQualifiedName~TourismObservationApiContractTests"`を実行する。
- [ ] DTO・classifierを実装する。ホテルの定員・予約・宿泊数、公共交通の接続意味が証明できなければ、そのoptional metricだけUnavailableにする。訪問そのものを証明できないときA2以降を成立扱いしない。
- [ ] 対象DLL・時刻変換・観測対象と除外対象・consumer/writer・サンプリングの限界を契約に記録し、検証後コミットする: `feat: define tourism observation and facility contracts`。

### Task A2: 共有観測feedと観光セッション

**Files:** Create:
- `src/CS2AreaSystem/Core/Observation/AreaActivityObservationSystem.cs`
- `src/CS2AreaSystem/Core/Observation/AreaActivitySample.cs`
- `src/CS2AreaSystem/Modules/Tourism/TourismObservationSystem.cs`
- `src/CS2AreaSystem/Modules/Tourism/TourismVisitTracker.cs`
- `src/CS2AreaSystem/Modules/Tourism/TourismRuntimeSnapshot.cs`
- `tests/CS2AreaSystem.Tests/TourismVisitTrackerTests.cs`
- `tests/CS2AreaSystem.Tests/AreaActivityObservationTests.cs`
Modify: `src/CS2AreaSystem/Modules/Park/ParkVisitSystem.cs`（重複した市民走査を共有feedへ接続）、`src/CS2AreaSystem/Mod.cs`（共有feed順序）。

**Interfaces:** Produces:
- `AreaActivitySample`（Entity Citizen、Household、Building、Provider、bool Tourist、uint Frame、int Generation、bool QualifiedUse）。
- `AreaActivityObservationSystem.RegisterConsumer(int typeId, IAreaActivityConsumer consumer)` / `RemoveConsumer(int typeId)`。
- `IAreaActivityConsumer.OnSamples(IReadOnlyList<AreaActivitySample> samples, uint frame, int generation)`。全消費者へ一度生成した結果を渡す。CoreはPark/Tourism DTOを知らない。
- `TourismVisitTracker.Observe(in AreaActivitySample sample, ulong areaId, double hours)`、`Expire(double hours)`、`SeedExisting(int generation, double hours)`。Produces: Started／FacilityUsed／Completedの`TourismVisitEvent`（Citizen、AreaId、Tourist、Frame、Generation、Countable bool）。
- `TourismObservationSystem.TryGetSnapshot(ulong areaId, out TourismRuntimeSnapshot snapshot)`。snapshotにはA1施設情報、現在人数、24時間集計、履歴カバレッジ、CycleIdを置く。

- [ ] 失敗するテストを書く。`TwoFacilitiesAreOneAreaSession`は施設利用2・完了エリア訪問1。`PassThroughIsNotAVisit`は0。`SeededVisitCompletionIsNotCounted`はロード初期利用の完了も0。
- [ ] 共有feedでPark/Tourismの重複対象を一度だけ走査し、`SharedFeedPreservesParkVisitAndAdmissionContracts`で従来Parkの開始・終了・料金イベント列が同じであることをassertする。
- [ ] `FullyQualifiedName~TourismVisitTrackerTests|FullyQualifiedName~AreaActivityObservationTests`を共通dotnet testコマンドでFAIL確認する。
- [ ] ネイティブUpdateFrameとscheduled Jobを使う観測を実装する。元のParkのprovider・在庫・inactive判定を保つ。必要な汎用helperはCoreへ移し、CoreからPark参照を作らない。
- [ ] 施設は128件/update上限のround-robin、観光客は共有feedの1周期を1ゲーム時間以内にする。宿泊世帯の別観測が必要なら観光世帯のsubsetを使い、2本目の全市民走査を追加しない。
- [ ] セッションは1ゲーム時間の非観測で終了。別エリアへ移ると前を終了する。各利用・終了イベントは一度だけ消費し、consumerの失敗を隔離する。
- [ ] preload前のpending Job drain・世代遮断、破棄・consumer解除を実装し、Park訪問・入園料・loadの既存テストを含めて共通検証を通す。コミット: `feat: share real activity observation with tourism visits`。

### Task A3: 評価・欠測・改善項目

**Files:** Create:
- `src/CS2AreaSystem/Modules/Tourism/TourismQualityModel.cs`
- `src/CS2AreaSystem/Modules/Tourism/TourismQualitySystem.cs`
- `docs/verification/tourism-score-examples.json` / `tourism-score-examples.md`（同じディレクトリ）
- `tests/CS2AreaSystem.Tests/TourismQualityModelTests.cs`

**Interfaces:** Consumes: A1施設、A2集計。Produces:
- `TourismQualityInputs`（long CompletedVisits24Hours、int OperatingFacilities、CategoryCount、TourismMetric AttractivenessSum、RoadPathCoverage、PublicTransportCoverage、bool ActivityAvailable）。
- `TourismQualityResult TourismQualityModel.Compute(in TourismQualityInputs inputs)`。Result: 4Metric Activity/Attractiveness/Variety/Accessibility、TourismMetric Overall、bool Provisional、IReadOnlyList<TourismDiagnostic> Diagnostics（最大3）。
- `TourismDiagnostic`（string ReasonKey、TourismMetric Evidence、string DenominatorLabel、uint SampleFrame）。

- [ ] 失敗する計算例を書く。10完了訪問／2施設→活動50、魅力度200→魅力50、カテゴリ2→多様性50。道路接続100・交通0→アクセス70、全有効の総合54。
- [ ] `MissingActivityBlocksXpButMissingLodgingDoesNotLowerScore`、`RealZeroDoesNotBecomeUnavailable`、`MissingTransitRenormalizesRoadAccess`を追加する。
- [ ] `FullyQualifiedName~TourismQualityModelTests`をFAIL確認する。
- [ ] 仕様書8節の式と30/30/20/20重みを実装する。Unavailableは再正規化、活動欠測ならProvisional、施設ゼロなら評価取得不可。ホテルの稼働率を総合式に追加しない。
- [ ] 合成例をsynthetic=trueで保存し、モデルから検証する。運営ONだけで診断を改善済みにしない。PASS後コミット: `feat: evaluate observed tourism quality transparently`。

### Task A4: 状態、XP、レベルとロード移行

**Files:** Create:
- `src/CS2AreaSystem/Modules/Tourism/TourismAreaState.cs`
- `src/CS2AreaSystem/Modules/Tourism/TourismHistory.cs`（TourismHistoryBucket : IBufferElementData, ISerializable。long HourBucket、long CompletedVisits/FacilityUses/ResidentUses、double CoveredHours、int Generation。時間集計はbufferとし、managed listをIComponentDataに入れない）
- `src/CS2AreaSystem/Modules/Tourism/TourismProgressionModel.cs`
- `src/CS2AreaSystem/Modules/Tourism/TourismProgressionSystem.cs`
- `src/CS2AreaSystem/Modules/Tourism/TourismRuntimeSystem.cs`
- `src/CS2AreaSystem/Modules/Tourism/TourismSavedStateValidator.cs`
- `tests/CS2AreaSystem.Tests/TourismProgressionModelTests.cs`
- `tests/CS2AreaSystem.Tests/TourismSerializationTests.cs`
Modify: `src/CS2AreaSystem/Modules/Tourism/TourismAreaModule.cs`、`src/CS2AreaSystem/Core/Areas/AreaCoreServicesSystem.cs`、`src/CS2AreaSystem/Mod.cs`、`tests/CS2AreaSystem.Tests/TourismBoundaryContractTests.cs`、`scripts/test-offline-contracts.mjs`。

**Interfaces:** Produces:
- `TourismAreaState : IComponentData, ISerializable`（ushort SchemaVersion、long CompletedVisitsLifetime/LastRewardedHour、uint XpDay、double XpFraction、int XpToday、bool NeedsSeed）。保存済み24時間集計はTourismHistoryBucket bufferを使う。
- `TourismHourObservation`（long HourBucket、uint Day、double Coverage、TourismMetric Quality、bool Provisional、int NewQualifiedFacilityUses）。
- `TourismXpResult TourismProgressionModel.SettleHour(ref TourismAreaState state, ref ManagedAreaProgress progress, in TourismHourObservation hour)`。
- `int TourismProgressionModel.EvaluatePromotion(in TourismPromotionInputs inputs)`。`TourismPromotionInputs`: int CurrentLevel/CategoryCount、long Experience/CompletedVisitsLifetime、TourismMetric Quality、double ValidObservedHours。`TourismXpResult`: int Awarded、string ReasonKey。両DTOはProgressionModel.csに定義する。
- `TourismRuntimeSystem.Ready` / `LoadGeneration` / `TryGetClock(out TourismClock clock)` / `TryGetSnapshot(ulong areaId, out TourismRuntimeSnapshot snapshot)`。

- [ ] `HourXpRequiresObservedUseAndSeventyFivePercentCoverage`で評価80、coverage .75、利用1→8XP、coverage .74や利用0→0。`ReloadDoesNotRepeatHourXp`は同一hour二度目0。
- [ ] 日上限240・時間上限10・端数繰越・18時間未満昇格不可・ホテルなし4カテゴリ昇格可能・永久レベルをassertする。
- [ ] `FullyQualifiedName~TourismProgressionModelTests|FullyQualifiedName~TourismSerializationTests`をFAIL確認する。
- [ ] 仕様書9節のXP/訪問/カテゴリ/評価閾値（XP 1000/5000/15000/35000、訪問100/500/2000/5000、カテゴリ2/3/3/4、評価40/55/70/80）をそのまま実装する。旧Tourism境界はOFF・累計0で移行し、全所属・全市民リストを保存しない。
- [ ] 新しいruntime ownerを登録単位に含め、preload世代resetを明示する。schema validatorを接続し未知schemaは隔離する。保存済み集計と再構築するderived観測を区別する。
- [ ] 旧境界テストの「1ファイルのみ／状態なし／Overviewのみ」を意図的に観光v1へ更新する。stableTypeId=3、他モジュールへの非混入・同種重複の非二重集計は保持する。
- [ ] common FocusedManagement等の既存XP条例を観光に勝手に追加しない。共通検証とsource guardsを通す。コミット: `feat: persist tourism growth without reload duplication`。

### Task A5: 6タブと権威ある操作・ローカライズ

**Files:** Create:
- `src/CS2AreaSystem/Modules/Tourism/TourismGameplayFacade.cs`
- `src/CS2AreaSystem/Modules/Tourism/TourismPanelBuilder.cs`
- `src/CS2AreaSystem/Modules/Tourism/TourismLocalization.cs`
- `tests/CS2AreaSystem.Tests/TourismPanelBuilderTests.cs`
- `tests/CS2AreaSystem.Tests/TourismPanelFixtureTests.cs`
- `UI/src/__fixtures__/tourism-panel.json`
- `UI/src/TourismPanel.test.tsx`
Modify: `src/CS2AreaSystem/Modules/Tourism/TourismAreaModule.cs`、`src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`。UI/src/ModulePanel.tsx・bindings.tsは既存row/bar/slider/buttonで表現できるため原則変更しない。

**Interfaces:** Consumes: A4 Runtime、C3/C4 Snapshot・復元、O2〜O4の設定・状態・精算。Produces:
- `TourismPanelModel TourismGameplayFacade.Read(Entity area, in AreaPanelContext context)`。
- `bool HandleAction(Entity area, string action, string value, in AreaPanelContext context)`。actionはSetPromotion／SetCirculation／SetLodging／RestoreCommercial／ConfirmRestoreCommercial／CancelRestoreCommercial。
- `AreaPanelDocument TourismPanelBuilder.Build(TourismPanelModel model)`。tab IDs: overview、visitors、lodging、facilities、operations、progress。ShowsProgress=true。
- 操作modeは既存sliderのmin0/max2/step1、C#で0/1/2を再検証し表示はOFF/標準/強化。復元は確認前には状態を変更しない。

- [ ] パネルにDesiredとEffectiveの差、費用予測と実精算、ホテルなし・取得不可・履歴不足・既存商業例外・復元進行を含むFAILするfixtureテストを書く。
- [ ] `StaleStableIdCannotChangeOperation`、`ForgedModeAndUnavailableAdapterAreRejected`、`RestoreRequiresConfirmation`をfacadeテストにする。
- [ ] C#のguarded moduleActionを使い、操作時にload generation・安定ID・可用性を再確認する。rowやsliderをUIで計算して権威にしない。
- [ ] 英日ローカライズをAllKeysから検証する。住民利用、施設利用、完了エリア訪問、空室、予約世帯を別のラベルにする。
- [ ] PowerShellで`$env:UPDATE_UI_FIXTURES='1'`を設定し、`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug -p:AreaApiCheck=true --filter "FullyQualifiedName~TourismPanelFixtureTests"`でfixtureを生成。環境変数を解除して再実行し、本文一致を確認する。
- [ ] UIで`npm test -- --run`と`npm run build`を通し、操作送信・disabled状態・6タブをfixtureから検証する。コミット: `feat: display tourism management in native selected info`。

### Task A6: 全体の実行時検証と性能・回帰

**Files:** Create: `docs/verification/tourism-area-v1-execution-record.md`。Modify: `scripts/runtime-smoke-checklist.md`、`README.md`（Tourism境界のみの説明を実際の検証状態に合わせる）。

**Interfaces:** Consumes: C5・O5の記録、A1〜A5。Produces: 公開できる完成判定と、未実施／不成立の正確な残件。

- [ ] 仕様書14節の18項目を、C5・O5との参照付きでチェックリストへ落とす。各結果に環境・保存データ・観測カバレッジを記録する。
- [ ] 日帰り観光地、宿泊中心、観光客ゼロ、住民のみ、複数エリア重複、公園内観光、エリア作成・削除・rename・ロードを実行する。
- [ ] 共有feed導入前後でParkの訪問・入園料・save/loadを回帰確認する。Industrialの実観測・条例不可状態を勝手に変えない。
- [ ] 大都市でsteady-stateの総処理時間・メインスレッド待ち・更新周期・対象数・メモリを測る。追加の全市民走査がないことをJob対象数と実計測で確認する。
- [ ] A3の合成計算例と実際のscore説明、A4の時間・日上限、ロード二重加算抑止を比較する。
- [ ] 共通検証と公式Toolchainビルドを実行し、実プレイ用DLL/UIの新しい出力を確認する。未実行項目をREADMEで実装検証済みと書かない。
- [ ] 全体レビューにC5・O5・A6の残件を添え、コミットする: `test: verify tourism integration and runtime behavior`。

## 共通の検証コマンド

リポジトリのルートで実行する。DLL・Toolchainの場所は実行環境のCSII_*変数に設定し、個人の絶対パスをコミットしない。

```powershell
dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug -p:AreaApiCheck=true --filter "FullyQualifiedName~Tourism"
./scripts/verify.ps1 -ApiCheck
```

UIの初回だけ`UI`で`npm ci`を実行する。UI変更後は`npm test -- --run`、`npm run build`。製品検証は公式Toolchain環境で`./scripts/verify.ps1`を実行する。Expected: すべてexit 0、NUnit failures 0。ゲーム実行結果は別の記録に残す。

各実装タスクはテストが失敗することを確認し、最小実装で通してから、そのタスクのFilesだけをstageしてコミットする。最初から通ったテストをred-greenの証拠としない。API探索のタスクは機能の不成立を「失敗したテストを通す」ために隠さず、契約を確かめる診断を作成する。
