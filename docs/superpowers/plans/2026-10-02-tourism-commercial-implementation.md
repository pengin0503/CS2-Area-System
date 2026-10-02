# Tourism Area v1 — 観光商業 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 通常商業を観光エリア内で自動的に観光業種へ限定し、新築・成長・入居・解除・保存の整合性を保つ。

**Architecture:** 元のprefabを変更せず、許可業種を絞った派生prefabと内部ゾーンを用いる方式を最初に検証する。セルは元の指定と所有世代を保持して差分で変換・復元する。方式が成立しない場合は代替を比較し、承認済み要求を満たす設計へ更新してから後続タスクを実行する。

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

- 境界をまたぐ敷地は対象セルと通常セルをまとめない（C3: CrossBoundaryLotDoesNotBypassRestriction）。
- 建物・会社がレベルアップや再入居しても一般業種に戻らない（C2/C5: UpgradeAndTenantCandidatesStayRestricted）。
- ユーザーの後続の塗り替えを解除処理で戻さない（C4: RestoreSkipsUserRepaintedCells）。
- 複製したコンポーネントの参照共有で都市全体を変更しない（C2: CloneFilterDoesNotMutateSource）。
- 建設決定後に境界が変わっても条件を再検証する（C3: BoundaryChangesDuringConstructionRevalidate）。

---

## ファイル構成

`Commercial/`は`src/CS2AreaSystem/Modules/Tourism/Commercial/`。prefab登録、セルの計算、ネイティブ適用、復元・保存を分離する。観測・運営の内部クラスに依存しない。

### Task C1: ネイティブ成長・会社入居・保存の成立性

**Files:** Create: `docs/verification/tourism-commercial-contracts.md`、`tests/CS2AreaSystem.Tests/TourismCommercialApiContractTests.cs`。

**Interfaces:** Consumes: Game 1.6.2f1のZoneSpawnSystem、ZoneCheckSystem、建物レベルアップ、CommercialSpawnSystem、CommercialFindPropertySystem、prefabシリアライズ。Produces: バージョン固定の契約文書と、採用／不成立の判定。後続C2の内部方式を採用する前提を証明する。

- [ ] 対象DLLを読み、m_AllowedSold、m_ZonePrefab、m_ZoneTypeの型・参照者・書込み所有者を調べる。会社の再入居と建物レベルアップの候補制限まで追う。
- [ ] NUnitの`GameHasRequiredZoningAndResourceContracts`で4Resource・型・フィールドの実在を固定する。共有Asset/Zoneの数値indexや私有Queueの推測を使わない。
- [ ] 公式Toolchainで検証用の2エリア・3敷地を用意し、派生候補の登録、通常ゾーンからのセル変換、新築・成長・空室への入居、ZoneCheck、保存再読込を試す。
- [ ] 実行資産一覧で密度・テーマ・レベル・サイズごとの候補を記録する。存在しないDLC資産を前提にしない。
- [ ] 文書に実際の登録時点、Job/Barrier依存、ゾーン・prefab再対応付け、通常商業への復元手段を具体名で記録する。実行未実施は未実施とする。
- [ ] 判定を記録してコミットする: `docs: pin tourism commercial native contracts`。不成立ならC2〜C5を止め、局所フィルタ方式との比較を設計へ反映する。

### Task C2: 許可業種と派生prefabの登録

**Files:** Create:
- `src/CS2AreaSystem/Modules/Tourism/Commercial/TourismCommercialContracts.cs`
- `src/CS2AreaSystem/Modules/Tourism/Commercial/TourismCommercialFilter.cs`
- `src/CS2AreaSystem/Modules/Tourism/Commercial/TourismCommercialPrefabSystem.cs`
- `tests/CS2AreaSystem.Tests/TourismCommercialFilterTests.cs`
- `tests/CS2AreaSystem.Tests/TourismCommercialPrefabTests.cs`

**Interfaces:**
- Produces: `static Resource TourismCommercialFilter.Restrict(Resource allowedSold)`、`static bool IsEligible(Resource allowedSold)`。許可集合とのbitmask積集合。空集合は不適格。
- Produces: `TourismCommercialPrefabSystem.TryResolve(Entity originalZone, out Entity restrictedZone)`、`TryGetOriginal(Entity restrictedPrefab, out Entity originalPrefab)`。準備前・候補不足はfalse。安定した名前と元prefab対応を保持する。
- Produces: `TourismCommercialSnapshot`（bool Ready、int RestrictedCells、LegacyBuildings、EligiblePrefabs、string ReasonKey）。

- [ ] 失敗するテストを書く:
```csharp
Assert.That(TourismCommercialFilter.Restrict(Resource.Lodging | Resource.Food), Is.EqualTo(Resource.Lodging));
Assert.That(TourismCommercialFilter.IsEligible(Resource.Petrochemicals), Is.False);
Assert.That(TourismCommercialFilter.Restrict(Resource.Meals | Resource.Entertainment | Resource.Recreation),
    Is.EqualTo(Resource.Meals | Resource.Entertainment | Resource.Recreation));
```
  `CloneFilterDoesNotMutateSource`で複製の販売集合を変更しても元の集合・ゾーン・資産参照が不変であることを確認する。
- [ ] `dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug -p:AreaApiCheck=true --filter "FullyQualifiedName~TourismCommercialFilterTests|FullyQualifiedName~TourismCommercialPrefabTests"`を実行。Expected: 新しい型がなくFAIL。
- [ ] C1で成立した登録時点にprefabを複製し、名前を元のprefab名＋固定された観光接頭辞で安定化する。モデルを新規生成せず、密度・テーマ・レベル・資産パック条件を保持する。
- [ ] `UpgradeAndTenantCandidatesStayRestricted`で複製後の販売集合に不適格Resourceが一つも残らないことを固定する。業種に応じた入力資源は勝手に削除しない。
- [ ] 上のNUnitフィルタとAPI-checkを通し、タスクFilesをコミットする: `feat: register restricted tourism commercial prefabs`。

### Task C3: セル・敷地の所属と自動適用

**Files:** Create:
- `src/CS2AreaSystem/Modules/Tourism/Commercial/TourismCommercialCellPolicy.cs`
- `src/CS2AreaSystem/Modules/Tourism/Commercial/TourismCommercialZoneSystem.cs`
- `tests/CS2AreaSystem.Tests/TourismCommercialCellPolicyTests.cs`

**Interfaces:** Consumes: C2のprefab resolver、Coreのネイティブ境界検索とprimary所属。Produces:
- `TourismCellDecision TourismCommercialCellPolicy.Evaluate(in TourismCellFacts facts)`。`TourismCellFacts`: Entity OriginalZone/CurrentZone、bool Occupied/UnderConstruction/IsCommercial/LegacyParcel、ulong ContainingAreaId/BoundaryToken、int Generation。`TourismCellDecision`: Kind(enum Normal/Restricted/WaitBoundary/WaitPrefabs/PreserveLegacy)、ulong AreaId/BoundaryToken、int Generation。これらのDTOはC2のContracts.csに定義する。
- `TourismLotDecision TourismCommercialCellPolicy.EvaluateLot(IReadOnlyList<TourismCellDecision> cells)`。Decisionは上のKind、AreaId、BoundaryTokenを持つ。通常／観光制限／境界待機／候補準備待機を区別する。
- `TourismCommercialZoneSystem.GetSnapshot(ulong areaId)` returns C2のスナップショット。

- [ ] `CrossBoundaryLotDoesNotBypassRestriction`で内部・外部セル混在時の待機、全内部なら観光、全外部なら通常をassertする。`BoundaryChangesDuringConstructionRevalidate`で古いtokenの建設決定をそのまま完成させない。
- [ ] `FullyQualifiedName~TourismCommercialCellPolicyTests`がFAILになることを確認する。
- [ ] 純粋policyを実装し、C1で確認したネイティブ更新・建設確定より前の時点に、差分セルのみ適用する。単なる遅い定期塗替えで先に一般店舗が建つ競合を許さない。
- [ ] 元の指定を変換前に保持し、同種重複・変更された境界・ロード世代で二重変換しない。既存建物・その敷地はC4の例外から除外しない。
- [ ] API-checkとpolicyテストを通し、コミットする: `feat: apply tourism restrictions to area commercial cells`。

### Task C4: 既存商業、復元、保存とロード

**Files:** Create:
- `src/CS2AreaSystem/Modules/Tourism/Commercial/TourismCommercialRestoreModel.cs`
- `src/CS2AreaSystem/Modules/Tourism/Commercial/TourismCommercialState.cs`
- `src/CS2AreaSystem/Modules/Tourism/Commercial/TourismCommercialRestoreSystem.cs`
- `tests/CS2AreaSystem.Tests/TourismCommercialRestoreTests.cs`
- `tests/CS2AreaSystem.Tests/TourismCommercialSerializationTests.cs`
Modify: `src/CS2AreaSystem/Mod.cs`（観光商業の一括登録・preload/serialize順序）。

**Interfaces:** Consumes: C2の元prefab対応、C3のtoken。Produces:
- `RestoreDecision TourismCommercialRestoreModel.Evaluate(in RestoreFacts facts)`。`RestoreFacts`: Entity OriginalZone/CurrentZone/ExpectedRestrictedZone、ulong OwnerAreaId/ExpectedOwnerAreaId、bool OriginalExists/ReferencesReady。`RestoreDecision : byte`: Restore/SkipUserEdit/SkipOtherOwner/WaitReferences/ReportMissingOriginal。型はRestoreModel.csに定義する。
- `bool TourismCommercialRestoreSystem.BeginRestore(ulong areaId)`、`bool IsRestoreComplete(ulong areaId)`。
- `TourismCommercialState : IComponentData, ISerializable`と必要なセル対応buffer。元prefab・zone Entityはネイティブの参照再対応付けを使う。

- [ ] `RestoreSkipsUserRepaintedCells`、`LegacyBusinessSurvivesAreaCreationAndUpgrade`、`VacatedLegacyParcelBecomesRestrictedOnlyAfterDemolition`をFAILするテストとして書く。
- [ ] 実装する。指定前からある敷地は解体まで元の成長・入居規則を保持する。ユーザーが塗り替えたセルを古い記録で戻さない。
- [ ] `RestoreKeepsCompanyAndReturnsOriginalPrefab`で元prefabへ戻す場合も会社・資産・レベル・敷地とZoneCheckの整合性をC1の手順で維持する。確認できない保存状態は成功扱いしない。
- [ ] 未知schema拒否、エリア削除後の復元、途中保存、ロード中Job drain、移行時の既存建物例外をテストする。旧Entity・世代を復元後へ持ち越さない。
- [ ] 共通検証を通し、コミットする: `feat: persist and restore tourism commercial designation`。

### Task C5: 成長・解除の実行時検証

**Files:** Create: `docs/verification/tourism-commercial-execution-record.md`。Modify: `scripts/runtime-smoke-checklist.md`（観光商業節）。Product codeの修正が必要なら該当するC2〜C4のテストへ戻る。

**Interfaces:** Consumes: C1〜C4。Produces: 各シナリオの実施日時、ゲーム版、資産条件、期待・実結果、log・save・スクリーンショットの参照、Pass/Fail/Not run。

- [ ] 仕様書14節の商業に関するケースを、エリア内外・境界上・需要ゼロ・候補ゼロ・テーマ／サイズ／密度・新築／成長／再入居で実行する。
- [ ] 建設途中の境界変更、エリア削除、手動塗替え、保存再読込、観光指定解除を実行する。
- [ ] 復元完了後に元prefab対応と内部ゾーン参照が残っていないことを確認して保存する。MODを外した直接読込の無条件互換性とは区別する。
- [ ] 不適格業種が一件でも新築・入居すればFail。Not runはPassにしない。
- [ ] 実行記録とチェックリストをコミットする: `test: record tourism commercial runtime verification`。

## 共通の検証コマンド

リポジトリのルートで実行する。DLL・Toolchainの場所は実行環境のCSII_*変数に設定し、個人の絶対パスをコミットしない。

```powershell
dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug -p:AreaApiCheck=true --filter "FullyQualifiedName~Tourism"
./scripts/verify.ps1 -ApiCheck
```

UIの初回だけ`UI`で`npm ci`を実行する。UI変更後は`npm test -- --run`、`npm run build`。製品検証は公式Toolchain環境で`./scripts/verify.ps1`を実行する。Expected: すべてexit 0、NUnit failures 0。ゲーム実行結果は別の記録に残す。

各実装タスクはテストが失敗することを確認し、最小実装で通してから、そのタスクのFilesだけをstageしてコミットする。最初から通ったテストをred-greenの証拠としない。API探索のタスクは機能の不成立を「失敗したテストを通す」ために隠さず、契約を確かめる診断を作成する。
