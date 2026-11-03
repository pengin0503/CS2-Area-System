# 観光運営の判断点 — 1.6.2f1 ネイティブ契約（Tourism v1 Task O1、静的調査）

状態: **静的調査のみ完了。計画O1の「通常動作を維持した限定probe」は NOT RUN。**

- 調査日: 2026-10-11
- 資料: `CS2-All-Managed-1.6.2f1-fix.zip` の `Game.dll`（SHA-256 先頭 `aaee15c4fa41c130`）、ilspycmd 9.1 の全体逆コンパイル。
- 契約テスト: `tests/CS2AreaSystem.Tests/TourismFeasibilityApiContractTests.cs`。

## 結論

1. **仕様6の「相対重み×1.15／×1.30／×1.10／×1.20」は1.6.2f1のネイティブ判断に存在しない概念で、そのままでは実装できない。** 観光客の目的地はすべて経路探索の最小コストで決まり、候補ごとの重みやコストを外から変える公開の入口が無い（§1, §2）。
2. 公開ECSだけで成立し得るのは「一部の判断をMODが選んだ目的地へ振り替える（リダイレクト）」方式である（§3）。これは仕様の効果の意味（倍率→振替確率）を変えるため、**設計変更としてユーザーの判断が必要**。
3. リダイレクトには、到達不能な目的地を選んだときに観光客が都市を去る既知の失敗経路がある（§3.2）。

| 効果 | 仕様どおりの倍率 | リダイレクト方式 |
|---|---|---|
| プロモーション（最初の観光先） | Rejected | 静的に経路あり・NOT RUN |
| 回遊促進（次の余暇先） | Rejected | 静的に経路あり・NOT RUN |
| 宿泊誘導（ホテル選択） | Rejected | 静的に経路あり・NOT RUN |

## 1. 観光客の判断点

観光世帯と観光客の行き先は、次の3つの別々の経路で決まる。仕様6.1〜6.3の区別とは一致しない。

### 1.1 世帯の目的地とホテル（TouristFindTarget）

- `TouristHouseholdBehaviorSystem`（GameSimulation、間隔64）が、建物を `Target` に持たない観光世帯へ `LodgingSeeker` を付ける（`TouristHouseholdBehaviorSystem.cs:42-90`）。ホテル予約済みの世帯にも付く。
- `TouristFindTargetSystem`（GameSimulation、間隔16）が `TouristHousehold + LodgingSeeker`、`MovingAway`・`Target` なしの世帯について経路探索を依頼する（`TouristFindTargetSystem.cs:69-111`）。目的地型は `SetupTargetType.TouristFindTarget`。
- 候補とコストは `CitizenPathfindSetup.SetupTouristTargetJob`（private、Burst、`CitizenPathfindSetup.cs:30-90`）が決める。候補は `LodgingProvider` または `AttractivenessProvider` を持つEntity。
  - 世帯がホテル未予約なら、空室のあるホテルはコスト `-5000 - 10×空室 + min(料金, 500)`、それ以外は `+5000`。つまり**未予約の世帯はほぼ必ずホテルを選ぶ**。
  - 予約済みなら全候補 `+5000`。
  - どちらも道路のAttractiveness供給量×0.01を引く。最終的な選択は移動コストとの合計が最小の1件。
- 結果が出ると同じジョブが、目的地がホテル会社なら予約キューへ、それ以外なら `AddMeeting(LeisureType.Attractions)` を積み、世帯に `Target` を付ける（112-141行）。目的地が無ければ `CitizenUtils.HouseholdMoveAway(..., MoveAwayReason.TouristNoTarget)` で**観光客を都市から去らせる**（142-145行）。
- ホテル予約 `HotelReserveJob`（158-197行）は、その時点の `m_FreeRooms > 0` を再確認してから空室を1つ減らし、`Renter` に世帯を追加し、`TouristHousehold.m_Hotel` を設定する。満室なら `LodgingSeeker` を付け直す。

したがって「最初の観光先」と「宿泊先」は同じ1回の判断で、仕様の「プロモーション」と「宿泊誘導」は同じ判断点に重なる。

### 1.2 観光名所への移動（Attraction）

- 世帯の `Target` があり未予約なら、`AddMeetingSystem` はその `Target` を会合の目的地 `CoordinatedMeeting.m_Target` にする（`Game.Events/AddMeetingSystem.cs:85-91`）。
- 目的地の無い会合では、`CitizenBehaviorSystem.AttendMeeting` が目的地なしの `TripNeeded` を積み、`TripNeededSystem` が `SetupTargetType.Attraction` で経路探索する（`TripNeededSystem.cs:1439-1442`）。
- `SetupAttractionJob` のコストは `-100 × Attractiveness × 乱数`（`CitizenPathfindSetup.cs:367-369`）。ここにはネイティブの乱数があるが、外から重みを足す入口は無い。

### 1.3 個人の余暇（Leisure）

- 観光客も住民と同じ `LeisureSystem`（GameSimulation）を使う。`SelectLeisureType` は観光客なら30%で `Attractions` を返し、残りは所得に応じた重み付き抽選（`LeisureSystem.cs:521-560`）。
- `Attractions`・`Sightseeing`・`Travel` は世帯の会合（§1.2）になる。それ以外（Meals・Entertainment・Commercial・City*）は `SetupTargetType.Leisure` で経路探索し、その種類の `LeisureProvider` のうち最小コストの1件を選ぶ（`LeisureSystem.cs:562-640`、`CitizenPathfindSetup.SetupLeisureTargetJob`）。
- 結果は `LeisureJob` が `Leisure.m_TargetAgent` と `TripNeeded` と `Target` に反映する（`LeisureSystem.cs:375-410`）。目的地が無ければ余暇を取り消してクールダウンを付けるだけで、都市を去ることはない。

仕様6.2「回遊促進」に最も近いのはこの経路である。

## 2. 重み・コストを変える公開経路が無い

- 目的地型ごとの候補列挙とコスト計算は `PathfindSetupSystem.FindTargets(SetupTargetType, in SetupData)`（private）の `switch` にあり、未知の型は警告を出して捨てる（`PathfindSetupSystem.cs:471-572`）。MODが型を追加したり、既存型のコストに項を足したりする入口は無い。
- 個々の Setup ジョブは private な Burst ジョブである。
- コストの入力（`AttractivenessProvider.m_Attractiveness`、`LodgingProvider.m_Price`・`m_FreeRooms`、道路の `ResourceAvailability`）を書き換えれば選択は変わるが、これらは都市全体の観光統計・需要・宿泊料金・他の住民の判断も読む共有状態である。仕様6.4の「観光評価・訪問数に直接加算しない」「空室の偽造・料金の無断変更をしない」に反するため採用しない。
- Harmony等でジョブを置換する方式はプロジェクト方針（バニラ処理のパッチ禁止）で除外。

## 3. リダイレクト方式（公開ECSで成立し得る経路）

ネイティブの判断が始まる前に、MODが一部の対象について目的地を決め、その目的地への経路探索だけを依頼する。結果の処理（予約・会合・余暇の開始）はネイティブに任せる。

### 3.1 使う公開API

- `PathfindSetupSystem.GetQueue(object, int, int)` → `NativeQueue<SetupQueueItem>`、`AddQueueWriter(JobHandle)`。
- `SetupQueueItem(Entity owner, PathfindParameters, SetupQueueTarget origin, SetupQueueTarget destination)`。目的地は `SetupTargetType.CurrentLocation` に `m_Entity = 選んだ会社または建物`。
- `PathfindTargetSeeker.FindTargets` は会社Entityを `PropertyRenter.m_Property` の建物へ解決し、結果の `PathInformation.m_Destination` には指定したEntity（会社）が入る（`PathfindTargetSeeker.cs:429`、`PathfindSetupSystem.SetupData.GetItem`）。
- `PathInformation { m_State = PathFlags.Pending }` を付けておくと:
  - `TouristFindTargetJob` は保留中の世帯を飛ばし、完了後に通常どおり予約・会合・`Target` を処理する（`TouristFindTargetSystem.cs:115-145`）。
  - `LeisureJob` は保留中の市民を飛ばし、完了後に通常どおり余暇を開始する（`LeisureSystem.cs:375-410`）。

### 3.2 失敗経路と制約

- **到達不能:** 世帯の振替先に経路が無いと `m_Destination` が Null になり、`TouristFindTargetJob` が `TouristNoTarget` で観光客を都市から去らせる。リダイレクトは、ネイティブ処理より前に自分の依頼結果を確認し、失敗なら `PathInformation` を外してネイティブの再探索に戻す必要がある。`TouristFindTargetSystem` は間隔16なので、MODのシステムは同じ間隔・同じオフセットで直前に置く必要がある（`UpdateBefore` と間隔の継承は `docs/verification/park-coverage-modifier-contract.md` §3 と同じ仕組み）。
- **余暇の目的地型:** `LeisureJob` は目的地prefabの `LeisureProviderData` を存在確認なしで読む（`LeisureSystem.cs:388`）。振替先は必ず `LeisureProviderData` を持つprefabの会社・建物でなければならない。
- **ホテル:** 振替先がホテルでも、予約時の空室再確認（`HotelReserveJob`）が残るので満室への予約は起きない。予約済み世帯は対象外にする。
- **意味の変化:** ネイティブは最小コストの1件を決定的に選ぶ。リダイレクトは「対象判断のうち一定割合をエリア内の候補へ振り替える」効果になり、仕様の「相対重み×1.15」とは別物。移動コストを比較しないので、遠い候補へ振り替えると移動時間が増える。
- **保存:** `PathInformation` は保存される。保存とロードをまたいだ振替依頼の結果は、MODの追跡情報（保存しない）が無い状態でネイティブに処理される。到達不能の結果がロード直後に出ると上の失敗経路に入る。
- **他MOD:** 同じ世帯・市民に先に `PathInformation` を付ける他MODとは競合する。既に `PathInformation` を持つ対象には触れない。

## 4. ユーザー判断が必要な点

- 運営効果を「倍率」ではなく「振替の割合」として定義し直すか。
- プロモーションと宿泊誘導が同じ判断点に重なることを受け入れ、2つを統合するか、別々の対象（未予約世帯のホテル選択／予約済み世帯の観光先）に分けるか。
- 到達不能時の再探索をMOD側で必ず行う前提で、ゲーム内の限定probe（振替前後の選択と移動時間の記録）を実施してから運営機能を有効にするか。
