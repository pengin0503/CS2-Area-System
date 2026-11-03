# Tourism Area v1 — 運営介入 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** プロモーション・回遊促進・宿泊誘導を実際の観光判断へ接続し、有効な稼働時間だけ都市資金から精算する。

**Architecture:** 設定・相対重み・適格性・費用を純粋モデルにし、ネイティブ判断との接続は効果別のアダプタへ分ける。判断が自由な機会にだけ作用し、既存予約・帰宅・所持金・到達可能性を尊重する。状態と実行証拠を観測・パネルへ公開する。

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

- 同一人物の判断を他MODが所有しているとき: 二重制御せず該当効果だけ停止（O3: ConflictingDecisionOwnerDisablesOnlyThatEffect）。
- 同一選択でプロモーションと回遊を二重乗算しない（O2: ContinuationUsesOnlyCirculationFactor）。
- OFF切替の直前まで稼働した費用は精算し、それ以後は課金しない（O4: MidIntervalOffSettlesOnlyPreviousActiveTime）。
- ロード前の精算済み・適用済み結果を新都市に再適用しない（O4: AppliedOldGenerationIsNotRequeued）。
- 満室・出発予定・既存予約・資金不足を効果が破らない（O3/O5: IneligibleTargetsRemainIneligible）。

---

## ファイル構成

`Operations/`は`src/CS2AreaSystem/Modules/Tourism/Operations/`。共通DTOはA1、実施設数はA2を使う。商業の内部ゾーンIDへ直接依存しない。

### Task O1: 判断点の成立性を3効果別に検証する

**Files:** Create: `docs/verification/tourism-operation-effect-contracts.md`、`tests/CS2AreaSystem.Tests/TourismOperationApiContractTests.cs`。

**Interfaces:** Consumes: TouristFindTargetSystem、Citizenの余暇目的地選択、PathfindSetupSystem、宿泊予約、TouristHouseholdとホテル会社。Produces: 各効果の実際の入口、出力、所有者、順序、原状復帰と競合条件を具体名で固定した契約文書。

- [ ] 3効果について、自然な初回観光判断・継続観光判断・宿泊検索の別々の処理を特定する。初回のTouristFindTargetだけを変更して回遊が完成したことにしない。
- [ ] 候補の元重み・到達可能性・費用・既存行動の判断条件を追う。公開経路を優先し、私有QueueやBurst Jobへの推測したパッチを前提にしない。
- [ ] 型・signature・phaseのAPI contract testを実際にコンパイルする。判断点の型が存在することと、稼働アダプタの成立を区別する。
- [ ] 通常動作を維持した限定probeで、対象外の判断は不変、対象候補だけ重みが変わることを記録する。ネイティブシステム全体の無効化・置換は採用しない。
- [ ] 公開経路で成立しなければ、小さなバージョン限定アダプタの対象・依存・保存と解除・他MOD競合を文書化する。新しい依存や大きな仕様変更は設計へ反映する。
- [ ] 各効果にVerified／Rejected／Not runと根拠を付ける。機能の必要条件が未成立ならO3を実装済みにせず止める。
- [ ] 契約テストと記録をコミットする: `docs: pin tourism decision and reservation effect contracts`。

### Task O2: 設定、重み、適格性、費用のモデル

**Files:** Create:
- `src/CS2AreaSystem/Modules/Tourism/Operations/TourismOperationContracts.cs`
- `src/CS2AreaSystem/Modules/Tourism/Operations/TourismOperationModel.cs`
- `tests/CS2AreaSystem.Tests/TourismOperationModelTests.cs`

**Interfaces:** Produces:
- enums `TourismOperationKind : byte { Promotion, Circulation, Lodging }`、`TourismOperationMode : byte { Off, Standard, Enhanced }`。
- `TourismOperationSettings`: 3つのMode（Promotion、Circulation、Lodging）。
- `TourismDecisionOpportunity`: Kind、Actor(Entity)、AreaId(ulong)、Generation(int)、HourBucket(long)、DecisionId(ulong)、IsFreeChoice、Eligible、HasReservation、Leaving、Affordable、Reachable、HasCapacity(bool)、CirculationChoicesToday(int)、OriginalWeight(double)。
- `double TourismOperationModel.Weight(in TourismDecisionOpportunity opportunity, TourismOperationMode mode)`。適格かつ運営稼働中なら対応倍率。OFFまたはCanApply=falseなら元の非負有限重みをそのまま返し、通常判断を消さない。元重み0は0、非有限・負の値は0。
- `double DailyUpkeep(TourismOperationKind kind, TourismOperationMode mode, int eligibleFacilities, int operatingHotels)`。
- `bool CanApply(in TourismDecisionOpportunity opportunity)`。

- [ ] 失敗するテストを書く:
```csharp
Assert.That(TourismOperationModel.DailyUpkeep(TourismOperationKind.Promotion, TourismOperationMode.Standard, 4, 1), Is.EqualTo(190));
Assert.That(TourismOperationModel.DailyUpkeep(TourismOperationKind.Circulation, TourismOperationMode.Enhanced, 4, 1), Is.EqualTo(240));
Assert.That(TourismOperationModel.DailyUpkeep(TourismOperationKind.Lodging, TourismOperationMode.Standard, 4, 2), Is.EqualTo(170));
```
  `WeightsMatchApprovedFactors`は3効果・3modeを比較し、`OffAndIneligibleOpportunityPreserveNativeWeight`でOFF・非対象の判断が元の重みのままであることをassertする。`ContinuationUsesOnlyCirculationFactor`は10から13になり16.9にはならないことをassertする。
- [ ] `dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug -p:AreaApiCheck=true --filter "FullyQualifiedName~TourismOperationModelTests"`。Expected: FAIL。
- [ ] 3費用式、倍率、OFF、不正mode拒否、非有限重み・負の件数の拒否、回遊上限2、予約・帰宅等をモデルに実装する。ホテルなしの宿泊は非稼働で費用0。
- [ ] 上のコマンドをPASSにしてコミットする: `feat: model tourism operating choices and costs`。

### Task O3: 効果別アダプタと実判断への接続

**Files:** Create:
- `src/CS2AreaSystem/Modules/Tourism/Operations/ITourismDecisionAdapter.cs`
- `src/CS2AreaSystem/Modules/Tourism/Operations/TourismPromotionAdapter.cs`
- `src/CS2AreaSystem/Modules/Tourism/Operations/TourismCirculationAdapter.cs`
- `src/CS2AreaSystem/Modules/Tourism/Operations/TourismLodgingAdapter.cs`
- `src/CS2AreaSystem/Modules/Tourism/Operations/TourismOperationSystem.cs`
- `tests/CS2AreaSystem.Tests/TourismDecisionAdapterTests.cs`
Native側への変更箇所はO1が固定した契約の入口に限定し、O1文書へ正確なパスを追記する。

**Interfaces:** Consumes: O2、A1の可用性、A2の施設・セッションスナップショット。Produces:
- `ITourismDecisionAdapter.Kind`、`bool TryEnable(int loadGeneration, out string reasonKey)`、`void Disable(int loadGeneration)`。ネイティブ橋渡しはO1の型に合わせる。
- `TourismOperationSystem.GetStatus(ulong areaId, TourismOperationKind kind)` returns `TourismOperationStatus`（Desired、Effective、AdapterVerified、FacilitiesReady、ReasonKey、EffectiveSinceFrame、LoadGeneration）。
- 変えた判断の結果のみを監査する`TourismDecisionAudit`（DecisionId、Kind、AreaId、OriginalWeight、EffectiveWeight、ChosenTarget、Generation）。選ばれる保証の表示には使わない。

- [ ] `ConflictingDecisionOwnerDisablesOnlyThatEffect`、`IneligibleTargetsRemainIneligible`、`RepeatedDecisionIdIsNotAppliedTwice`、`RemovingSettingLeavesNoSavedModifier`をFAILするadapter/controllerテストにする。
- [ ] テストfilter `FullyQualifiedName~TourismDecisionAdapterTests`でFAILを確認する。
- [ ] O1でVerifiedの経路だけを実装する。確定済みTarget・PathInformationやバニラが毎回再計算する魅力度を後から上書きして競合を隠す方式は禁止。
- [ ] 効果の世代・同種primary・設定を再確認し、適格判断にだけ倍率を渡す。観光客を生成・瞬間移動させない。回遊では同じ施設往復と日上限のリセットを防ぐ。
- [ ] failureを効果・エリア・世代単位で抑制し、未対応機能のEffectiveをfalseにする。設定Desiredは保持する。3種がVerifiedでないままv1完了とはしない。
- [ ] 共通API-checkとadapterテストを通しコミットする: `feat: connect area tourism operating effects to real decisions`。

### Task O4: 保存、実精算、停止とロード境界

**Files:** Create:
- `src/CS2AreaSystem/Modules/Tourism/Operations/TourismOperationState.cs`
- `src/CS2AreaSystem/Modules/Tourism/Operations/TourismOperationFinanceSystem.cs`
- `src/CS2AreaSystem/Modules/Tourism/Operations/TourismOperationSaveSystem.cs`
- `tests/CS2AreaSystem.Tests/TourismOperationFinanceTests.cs`
Modify: `src/CS2AreaSystem/Core/Finance/AreaFinanceKind.cs`、`AreaFinanceAccounts.cs`（同じディレクトリ）、`src/CS2AreaSystem/Mod.cs`。

**Interfaces:** Consumes: O3 status、A2施設数、AreaFinanceSystem.EnqueueExpenseとIAreaFinanceListener。Produces:
- append enum `AreaFinanceKind.TourismOperations = 3`（旧値0/1/2を変えない）、IsExpense判定・AreaManagement合計へ追加。
- `TourismOperationState : IComponentData, ISerializable`（schema、Desired設定、精算境界、端数、実精算累計）。Effectiveは再構築。
- `bool TrySetMode(Entity area, TourismOperationKind kind, TourismOperationMode mode, uint frame)`：旧設定でframeまで精算してから新設定に変える。
- `void TourismOperationFinanceSystem.FlushForSave()`：未精算期間を共通Financeへ送り、SaveSystemをAreaFinanceSaveSystemより前に置く。frame区間の比率には検証済みのAreaFinanceModel.FramesPerDayを使い、長い未観測区間へ遡って請求しない。

- [ ] `MidIntervalOffSettlesOnlyPreviousActiveTime`で標準プロモーション4施設を半日稼働後OFFにし、費用95・以後0をassertする。`UnavailableAdapterNeverAccrues`、`NegativeCityMoneyDoesNotSuppressExpense`も追加する。
- [ ] `AppliedOldGenerationIsNotRequeued`で旧世代で確定済みの費用は再enqueueされず、実精算listenerで一度だけ記録されることをassertする。
- [ ] `FullyQualifiedName~TourismOperationFinanceTests`がFAILになることを確認する。
- [ ] 時間比例・floor＋端数繰越・可用性変更時の区間分割を実装し、観測不明／施設なし／adapter停止中は新規課金しない。実費はlistenerの確定結果から記録する。
- [ ] 未完了Jobはpreload前に処理・遮断し、保存前は観光精算→共通Finance flush→Serializerの順序を登録する。登録失敗で観光効果だけ稼働して課金できない状態を作らない。
- [ ] 共通Finance回帰、保存round-trip、共通検証を通してコミットする: `feat: settle and persist tourism operating expenses`。

### Task O5: 効果と金額の実行時検証

**Files:** Create: `docs/verification/tourism-operations-execution-record.md`。Modify: `scripts/runtime-smoke-checklist.md`（運営節）。

**Interfaces:** Consumes: O3のAudit、O4の確定費用、A5の操作UI。Produces: OFF／標準／強化の試行結果と安全条件の実行記録。

- [ ] 固定した都市・施設条件で対象となる判断ごとに元重み・効果後重み・実選択を記録し、対象外の判断は不変であることを確認する。
- [ ] 3種それぞれでOFF／標準／強化を切り替え、選択重みの正確さを直接確認する。確率的な選択結果は、少なくとも各条件500件の適格判断を集めて件数・比率・不確実性を報告する。少数の訪問増加を効果成立の証拠にしない。
- [ ] 満室、帰宅予定、所持金不足、到達不能、予約済み、日回遊上限、他MODの判断所有を試す。
- [ ] 半日切替・停止・施設除去・負残高・保存ロードで実都市資金とArea Managementが一致することを確認する。
- [ ] 料金の二重徴収・バニラ収入の二重計上・解除後の永続補正がないことを確認して記録をコミットする: `test: record tourism operating runtime evidence`。

## 共通の検証コマンド

リポジトリのルートで実行する。DLL・Toolchainの場所は実行環境のCSII_*変数に設定し、個人の絶対パスをコミットしない。

```powershell
dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug -p:AreaApiCheck=true --filter "FullyQualifiedName~Tourism"
./scripts/verify.ps1 -ApiCheck
```

UIの初回だけ`UI`で`npm ci`を実行する。UI変更後は`npm test -- --run`、`npm run build`。製品検証は公式Toolchain環境で`./scripts/verify.ps1`を実行する。Expected: すべてexit 0、NUnit failures 0。ゲーム実行結果は別の記録に残す。

各実装タスクはテストが失敗することを確認し、最小実装で通してから、そのタスクのFilesだけをstageしてコミットする。最初から通ったテストをred-greenの証拠としない。API探索のタスクは機能の不成立を「失敗したテストを通す」ために隠さず、契約を確かめる診断を作成する。
