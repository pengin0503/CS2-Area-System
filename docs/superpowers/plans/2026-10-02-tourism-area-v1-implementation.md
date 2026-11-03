# Tourism Area v1 — 全体 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 観光向け商業と実際の観光行動に作用する運営機能を、観測・成長・保存・ネイティブUIへ統合する。

**Architecture:** 責任を観光商業、運営アダプタ、観測・成長の3計画に分ける。商業・行動経路の成立性を先に確認し、純粋モデルと公開DTOを介して統合する。初版の完了条件は3計画すべての成立と実行時検証である。

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

- 境界をまたぐ敷地: 通常店舗で穴埋めせず、内部セルを分離できなければ待機する（C3: CrossBoundaryLotDoesNotBypassRestriction）。
- 建設中・退去中の境界変更: 対象外へ制限が漏れず、不適格候補が内部へ完成しない（C3: BoundaryChangesDuringConstructionRevalidate）。
- ユーザーが後からゾーンを塗り替えた場合: 解除で古いセル状態を上書きしない（C4: RestoreSkipsUserRepaintedCells）。
- 保存中のロードと精算済み結果: 旧世代Job・課金・XPを新都市へ適用しない（O4/A4: AppliedOldGenerationIsNotRequeued、ReloadDoesNotRepeatHourXp）。
- 宿泊・購買の会社Entityと建物Entityの違い: 予約世帯や単なる滞在を購入・宿泊完了とみなさない（A1/A2: CompanyPropertyMappingDoesNotInventVisit）。

---

## 計画の分割と実行順序

| 計画 | 成果物 | 単体で検証できる範囲 |
|---|---|---|
| [観光商業](2026-10-02-tourism-commercial-implementation.md) | 専用候補、新築・成長・入居制限、解除 | エリア内外の建物・会社・ゾーンの整合性 |
| [運営介入](2026-10-02-tourism-operations-implementation.md) | 3効果の接続、設定、実精算 | 選択判断・可用性・費用の正確さ |
| [観測・成長](2026-10-02-tourism-observation-implementation.md) | 共有観測、評価、XP、保存、パネル | 実利用の集計、欠測、重複防止、UI |

1. **先行成立性:** C1・O1・A1。APIの型・書込み所有者・実行順序・資産・観測意味を確認する。
2. **基盤:** A2〜A4で観測DTOと保存・成長を実装する。同時にC2〜C4で観光商業を実装する場合も、共有ファイルの編集は順序化する。
3. **運営:** O2〜O4。A2の施設スナップショットを使い、Cの商業限定とは独立した可用性を持つ。
4. **UIと統合:** A5。O5・C5・A6の実行時検証を実施する。
5. 最後に全体の回帰・性能・通常商業への復元を確認する。

先行成立性が未成立なら、後続の製品アダプタや自動セル変換を推測で作らない。実行時環境が必要で試せない場合は、確認済みの範囲・未検証項目・必要なゲーム内手順を記録し、v1完成とはしない。

## 共有ファイルと責任

| ファイル | 所有する作業 | 変更の目的 |
|---|---|---|
| Modules/Tourism/TourismContracts.cs | A1 | 基本DTO、時計、施設カテゴリ、可用性 |
| Modules/Tourism/Commercial/TourismCommercialContracts.cs | C2 | セル・prefabの対応と変換結果 |
| Modules/Tourism/Operations/TourismOperationContracts.cs | O2 | 設定、判断機会、効果と可用性 |
| Core/Areas/AreaCoreServicesSystem.cs | A4 | 観光モジュールの構築 |
| Mod.cs | C4、O4、A4 | バンドル単位の登録とロード・保存順序 |
| Core/Finance/AreaFinanceKind.cs / AreaFinanceAccounts.cs | O4 | TourismOperationsの費用分類と合計 |
| Core/UI/NativeAreaLocalization.cs | A5 | 観光パネルのローカライズ |
| tests/TourismBoundaryContractTests.cs | A4 | 境界限定契約を観光v1契約へ更新 |
| scripts/test-offline-contracts.mjs | A4・A5 | 観光固有の制約追加。既存モジュールの制約は維持 |

上記の省略パスは`src/CS2AreaSystem/`、testsは`tests/CS2AreaSystem.Tests/`を基点とする。各サブ計画のFilesには完全なリポジトリ内パスを記載する。必要のないUI固有bindingや一般的なCore再設計は追加しない。

## 仕様と担当作業の対応

| 仕様書節 | 担当 |
|---|---|
| 1〜3 目的・分離・状態 | A1、A4、各計画のGlobal Constraints |
| 4〜5 観光商業・復元 | C1〜C5 |
| 6 運営・行動・費用 | O1〜O5 |
| 7 訪問・宿泊・ロード | A1〜A4 |
| 8 評価 | A3 |
| 9 成長 | A4 |
| 10 パネル | A5 |
| 11 性能・隔離 | C3、O3、A2、A6 |
| 12 保存・移行 | C4、O4、A4 |
| 13 順序 | この全体計画 |
| 14 検証 | C5、O5、A6 |
| 15 根拠・確度 | C1、O1、A1の契約文書 |

## 最終確認と引継ぎ

- [ ] 最新mainを取得し、計画基点との差分を確認して隔離ブランチを用意する。
- [ ] 3計画を読んだ上で、契約DTOの命名・署名を変更する場合は依存タスクと同時に更新する。
- [ ] 先行成立性の記録で「DLL上の存在」「APIコンパイル」「資産の実在」「ゲーム内の効果」を区別する。
- [ ] 実装完了時に共通検証を通し、実行記録の未実施を合格へ書き換えない。
- [ ] 観光運営の3効果、観光商業の全経路、通常商業への復元が成立してからv1完成とする。
- [ ] ブランチ全体をレビューし、ユーザーが承認した統合・実行方法に従う。

実装方法は計画レビュー時に確認する。これまでのプロジェクトで使われたNative実装への引継ぎにも使える形とし、特定モデルの存在を前提にしたコード・テストは持たない。計画の作成とレビューは製品実装の開始ではない。

## 共通の検証コマンド

リポジトリのルートで実行する。DLL・Toolchainの場所は実行環境のCSII_*変数に設定し、個人の絶対パスをコミットしない。

```powershell
dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug -p:AreaApiCheck=true --filter "FullyQualifiedName~Tourism"
./scripts/verify.ps1 -ApiCheck
```

UIの初回だけ`UI`で`npm ci`を実行する。UI変更後は`npm test -- --run`、`npm run build`。製品検証は公式Toolchain環境で`./scripts/verify.ps1`を実行する。Expected: すべてexit 0、NUnit failures 0。ゲーム実行結果は別の記録に残す。

各実装タスクはテストが失敗することを確認し、最小実装で通してから、そのタスクのFilesだけをstageしてコミットする。最初から通ったテストをred-greenの証拠としない。API探索のタスクは機能の不成立を「失敗したテストを通す」ために隠さず、契約を確かめる診断を作成する。
