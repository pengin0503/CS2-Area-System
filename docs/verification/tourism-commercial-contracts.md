# 観光商業 — 1.6.2f1 ネイティブ契約（Tourism v1 Task C1、静的調査）

状態: **静的調査のみ完了。ゲーム内の試行（計画C1の公式Toolchainによる2エリア・3敷地の試験）は NOT RUN。**

- 調査日: 2026-10-11
- 資料: 提供された `CS2-All-Managed-1.6.2f1-fix.zip` の `Game.dll`（SHA-256 先頭 `aaee15c4fa41c130`）を ilspycmd 9.1 で全体逆コンパイル。行番号はその出力のもの。
- 契約テスト: `tests/CS2AreaSystem.Tests/TourismFeasibilityApiContractTests.cs`（型・フィールド・定数・登録順のみ）。
- 「DLL上の存在」「APIコンパイル」「資産の実在」「ゲーム内の効果」を区別する。この文書は前の2つと、コード経路の読解までを扱う。

## 判定

| 項目 | 判定 | 根拠 |
|---|---|---|
| 派生ゾーン＋派生建物で新築候補を限定 | 静的に成立 | §1, §2 |
| レベルアップ後も限定を維持 | 静的に成立 | §3 |
| 入居会社を許可業種に限定 | 静的に成立 | §4 |
| 保存再読込でセルの派生ゾーンを維持 | 静的に成立（MOD有効時） | §5 |
| 既存商業の保持 | 条件付き（占有セルを変換しない） | §6 |
| 観光指定解除で建物・会社を保持したまま復元 | 静的に経路あり・順序の実証は NOT RUN | §7 |
| 実資産で許可業種の候補が存在する | NOT RUN（資産は未提供） | §8 |

## 1. 建物の販売業種はゾーンから決まる

- `SpawnableBuilding.m_ZoneType`（`ZonePrefab`）が建物の所属ゾーン。
- `SpawnableBuilding.GetPrefabComponents` / `Initialize` は `m_ZoneType.InitializeBuilding` を呼ぶ。
- `ZoneProperties.InitializeBuilding` は、建物prefabが独自の `BuildingProperties` を持たない限り、`BuildingPropertyData.m_AllowedSold = ZoneProperties.m_AllowedSold` を書く（`Game.Prefabs/ZoneProperties.cs:92-131`）。
- したがって派生ゾーンの `ZoneProperties.m_AllowedSold` を「元のゾーンの集合 ∩ {Lodging, Meals, Entertainment, Recreation}」にすれば、派生建物の販売集合は自動的にその積集合になる。
- 建物prefabが独自の `BuildingProperties` を持つ場合はゾーンの値を使わない。その建物は派生時に `BuildingProperties.m_AllowedSold` も同じ積集合へ絞る必要がある。
- `EconomyUtils.IsCommercialResource` は4種すべてで true（契約テストで確認）。`Resource.Food` 等の通常商品も true なので、許可集合は明示的な積集合でなければならない。

## 2. 新築候補の選択

`ZoneSpawnSystem.EvaluateSpawnAreas.SelectBuilding`（`Game.Simulation/ZoneSpawnSystem.cs:277-365`）:

- 候補prefabのチャンクを共有コンポーネント `BuildingSpawnGroupData.m_ZoneType` がセルのゾーン型と一致するものに限定する（288行）。
- `BuildingSpawnGroupData` は `BuildingInitializeSystem` が `ZoneData.m_ZoneType` から設定する（`BuildingInitializeSystem.cs:1024`）。
- レベル1、敷地・高さ、角地条件で絞り、`EvaluateDemandAndAvailability` で需要を計算する。商業では `m_AllowedSold` に含まれる各Resourceの `CommercialBuildingDemands` の和（372-394行）。
- 需要が `m_MinDemand` 未満の候補は選ばれない。許可業種に需要がなければ派生ゾーンのセルは空き地のまま残る（需要を偽造する必要も経路もない）。
- 都市全体で商業の新築は `CommercialDemandSystem.buildingDemand > 0` のときだけ評価される（1102行）。

派生ゾーンのセルには派生ゾーン型の建物しか建たない。元のゾーン型の候補はエリア外で従来どおり使われる。

## 3. レベルアップ

`BuildingUpkeepSystem.SelectSpawnableBuilding`（`BuildingUpkeepSystem.cs:761-792`）:

- 同じ `BuildingSpawnGroupData.m_ZoneType` のチャンクだけを走査する。
- さらに次レベル・同じ敷地サイズ・アクセス方向に加え、`m_AllowedSold`・`m_AllowedInput`・`m_AllowedManufactured`・`m_AllowedStored` が**完全一致**する候補だけを選ぶ。

派生建物を全レベル分そろえておけば、レベルアップ後も派生ゾーン・許可集合にとどまる。あるレベルの派生候補が欠けると、その建物はレベルアップできない（通常建物へ戻る経路はない）。

## 4. 入居会社

- 商業会社の物件探索は `PropertyUtils`（`Game.Buildings/PropertyUtils.cs:292`）で、会社の出力Resourceと物件の `m_AllowedSold` を照合する。
- `CommercialDemandSystem` は空き物件を `m_AllowedSold` のResourceごとに数える（157-165行）。派生建物の空き物件は許可業種の空き物件としてだけ数えられる。
- 宿泊は `CommercialDemandSystem` で特別扱いされる（182・205行）。`Lodging` の需要は観光客数×ホテル客室要求率と客室供給の差から決まる。
- 会社の撤退後の再入居も同じ照合を通るため、派生建物に一般業種の会社が入る経路は見つからなかった。

## 5. ゾーン型の採番と保存

- `ZoneSystem.InitializeZonePrefabs`（`Game.Prefabs/ZoneSystem.cs:139-210`）は、`Created` の `ZonePrefab` に実行時の連番 `ZoneType.m_Index`（ushort）を振る。MODが `PrefabSystem.AddPrefab` で後から追加したゾーンにも同じ経路で番号が付く。
- 保存されたセルは `Cell.m_Zone`（`ZoneType`、2バイト）を生の番号で書く。
- ロード時 `ResolvePrefabsSystem.FillZoneTypeArrayJob` / `FixZoneTypeJob`（`Game.Serialization/ResolvePrefabsSystem.cs:252-330, 463-516`）が、保存時のゾーンprefabと現在のprefabを対応付けて全セルと `VacantLot` の番号を張り替える。保存時の番号でなくprefab識別で対応するため、MODが固定の数値indexを持つ必要はない。
- 張り替え表は `new NativeArray<ZoneType>(340, ...)` の固定長。保存データ側のゾーン型番号が340以上だと範囲外になる。派生ゾーンはバニラのゾーン数＋派生分に収める必要がある（派生は元の商業ゾーン1つにつき1つ）。
- 対応する現在のprefabが無い（MODを外した）場合、そのセルは `ZoneType.None`（未指定）に変わる。仕様12節の「直接アンインストールの無条件互換性は約束しない」と一致する。
- 派生prefabはロード前に登録されていなければならない。現在の `AreaPrefabRegistrationSystem` と同じ `PrefabUpdate` フェーズ、`PrefabInitializeSystem` より前の登録経路が候補。

## 6. 既存商業と ZoneCheck

`Game.Buildings.ZoneCheckSystem.ValidateZoneBlocks`（`ZoneCheckSystem.cs:335-365`）:

- 建物の敷地のすべてのセルが、**その建物自身のゾーン型**と一致し、道路に面していなければ、建物に `Condemned` を付ける（301-313行）。
- 建物のゾーンprefabの `PrefabData` が無効な場合も不合格になる（337行）。
- 判定はゾーンセルが更新された範囲（`Game.Zones.UpdateCollectSystem` の更新範囲）だけで実行される。

帰結:

- 既存の通常商業の敷地セルを派生ゾーンへ書き換えると、その建物は Condemned になり、解体へ進む。仕様4.3「既存商業を自動撤去しない」を守るには、**占有セルは書き換えず、解体で空いた時点で変換する**必要がある。これは計画C3/C4の `PreserveLegacy` と一致する。
- 境界をまたぐ敷地: 1つの建物の敷地に通常セルと派生セルが混在すると、どちらのゾーン型の建物でも Condemned になる。仕様4.4の「内部セルを分離できなければ待機」は必須条件である。

## 7. 観光指定解除（通常商業への復元）

- 派生建物が立つセルを元のゾーンへ戻すと、§6 により派生建物が Condemned になる。
- 建物・会社を保持したまま戻すには、建物Entityの `PrefabRef` を元の建物prefabへ差し替える必要がある。
- バニラのレベルアップはこれを公開コンポーネントで行う。`BuildingUpkeepSystem`（`BuildingUpkeepSystem.cs:727-731`）が `UnderConstruction { m_NewPrefab = 次レベルprefab, m_Progress = 255 }` を付け、`BuildingConstructionSystem`（`BuildingConstructionSystem.cs:166-174, 222-225`）が同じEntityの `PrefabRef` を差し替えて `Updated` を付け、`UnderConstruction` を外す。Entityは作り直されないため、`Renter` と入居会社は残る。
- 派生建物の元prefab（同じレベル・敷地）を `m_NewPrefab` にすれば、同じ経路で元の建物へ戻せる見込みがある。元ゾーンの販売集合は派生の集合を含むので、入居中の観光会社は元の建物でも適格のまま。
- ただし順序が成立条件になる。セルを元のゾーンへ戻す更新より前にprefab差し替えが完了していないと、§6 の ZoneCheck が派生建物を Condemned にする。差し替え完了 → セル書き戻し、の順序とその間の保存を、ゲーム内で確認するまで成立とは扱わない。
- 確認できない場合の代替（どちらも仕様変更でありユーザー判断が必要）:
  1. 解除時は空きセルだけ戻し、派生建物は解体されるまで観光商業のまま残す。
  2. 解除時に派生建物を Condemned にして解体させる（破壊的）。

## 8. 実資産（NOT RUN）

- 提供物にはゲームの資産データが無い。バニラの商業ゾーン（密度・地域テーマ別）の `ZoneProperties.m_AllowedSold` に Lodging・Meals・Entertainment・Recreation がどれだけ含まれるか、各レベル・敷地サイズに候補建物があるかは確認できない。
- 特に、宿泊（Lodging）の会社prefabと、それを受け入れる商業建物が全密度にあるかは未確認。候補が無い組み合わせではセルが空き地のまま残る。
- 必要な実行時確認: ゲーム内で派生prefabを登録し、密度・テーマ・レベル・敷地サイズごとの派生候補数をログに出す。

## 9. UI

- 派生ゾーンに `UIObject` を付けなければ、ゾーンツールの一覧には出ない（ユーザーが直接塗れない）。
- ゾーンの色は `ZoneSystem.UpdateZoneColors` が prefab ごとに設定する。派生ゾーンを元と同じ色にするか、観光用に変えるかは表示上の選択。
