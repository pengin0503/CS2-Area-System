# 観光の観測経路 — 1.6.2f1 ネイティブ契約（Tourism v1 Task A1、静的調査の部分）

状態: **経路の静的調査のみ完了。計画A1のDTO・分類器（製品コード）は未着手。ゲーム内の観測精度は NOT RUN。**

- 調査日: 2026-10-11
- 資料: `CS2-All-Managed-1.6.2f1-fix.zip` の `Game.dll`（SHA-256 先頭 `aaee15c4fa41c130`）、ilspycmd 9.1 の全体逆コンパイル。
- 契約テスト: `tests/CS2AreaSystem.Tests/TourismFeasibilityApiContractTests.cs`。

## 1. 宿泊（仕様7.2の未検証点の解消）

| 値 | 経路 | 意味 |
|---|---|---|
| 客室数 | `LodgingProviderSystem.GetRoomCount(lotSize, level, BuildingPropertyData)`（public static、`LodgingProviderSystem.cs:264-267`） | `(int)(敷地セル数 × レベル × m_SpaceMultiplier)`。建物prefabから計算でき、推測は不要 |
| 宿泊中の世帯 | ホテル**会社**の `Renter` バッファ | 観光世帯だけが残る。会社の `LodgingProviderSystem` 更新で観光世帯以外は除去される（`LodgingProviderSystem.cs:113-119`） |
| 空室 | `LodgingProvider.m_FreeRooms` | `客室数 − Renter数`。1室＝1世帯（`LodgingProviderSystem.cs:156`） |
| 料金 | `LodgingProvider.m_Price` | 1日あたり1世帯の料金。`LeisureParametersData.m_TouristLodgingConsumePerDay × 市場価格`（`LodgingProviderSystem.cs:138-155`） |
| 世帯のホテル | `TouristHousehold.m_Hotel` | ホテル会社Entity。予約時に `TouristFindTargetSystem.HotelReserveJob` が設定する |
| 宿泊人数 | 世帯の `HouseholdCitizen` バッファ | 客室・世帯・人数は別の量。稼働率は「Renter数 ÷ 客室数」で、人数ではない |

補足:

- 客室数が減ると、超過分の世帯は `m_Hotel = Null` にされて追い出される（`LodgingProviderSystem.cs:120-131`）。
- 更新は `kUpdatesPerDay = 32`（UpdateFrame で分割）。`m_FreeRooms` は最大でゲーム内1日の1/32だけ古い。
- 予約済みかつ到着前の世帯も `Renter` に入る。`Renter` は「予約・宿泊中」であり、建物内に居ることの証明ではない。

## 2. 観光名所の訪問は Leisure だけでは確実に観測できない

- 観光世帯の観光名所への移動は、世帯単位の会合（`CoordinatedMeeting`、`LeisureType.Attractions` の会合prefab）で行われる（`Game.Events/AddMeetingSystem.cs`、`Game.Simulation/MeetingSystem.cs`）。到着後は `CoordinatedMeeting.m_Status = Attending` になり、全員がその建物に `CurrentBuilding` で居る間だけ続く（`MeetingSystem.cs:139-170`）。
- 会合を始める経路は2つある。`TouristFindTargetSystem` が世帯単位で積む場合は、各市民の `Leisure` の有無と無関係に会合が進む。`LeisureSystem.FindLeisure` が `Attractions` を選んで積む場合、市民の `Leisure` は外されずに残る（`LeisureSystem.cs:562-576`）。`CitizenBehaviorSystem` の会合処理（`AttendMeeting`）も `Leisure` を外さない（`CitizenBehaviorSystem.cs:765-768`）。
- `Leisure` が残っている市民が名所の建物に着くと、`LeisureJob` は `m_TargetAgent` が無くても、建物prefabに `LeisureProviderData` があればその建物で余暇を消費する（`LeisureSystem.cs:336-366`）。
- したがって既存の `ParkVisitSystem` と同じ `Citizen + Leisure + CurrentBuilding` の経路では、名所の訪問は経路によって数えられたり漏れたりする。観光の観測は「`Leisure` による余暇」と「`Attending` の会合で目的地の建物に居る」の両方を扱い、同じ市民を二重に数えない必要がある。どの割合で漏れるかは NOT RUN。

## 3. 商業施設の利用

- 飲食・娯楽・商業の余暇は `Leisure.m_TargetAgent` に**会社**Entityが入り、その会社の `PropertyRenter.m_Property` が建物（`LeisureSystem.cs:336-346`）。
- 利用中かどうかは、市民の `CurrentBuilding` がその建物であることで判断する。目的地を決めただけ（`Target` あり、移動中）の市民を利用と数えない。
- これは計画A1の `CompanyPropertyMappingDoesNotInventVisit` が想定する会社→建物の対応と一致する。

## 4. 観光客と住民

- 市民の区別は `Citizen.m_State` の `CitizenFlags.Tourist`（`CitizenBehaviorSystem.cs:650`）と、世帯の `TouristHousehold` の有無。既存の `ParkVisitSystem` は後者を使う。

## 5. 時刻

- 時刻の変換は既存の `IndustrialGameClock` と同じ検証済みの `TimeData` 経路を使う方針（計画A1）。この調査では新しい事実は無い。

## 6. 未確認（NOT RUN）

- 会合の `Attending` と `CurrentBuilding` の組で、実際に観光名所の訪問が過不足なく数えられるか。
- 予約から到着までの時間、`Renter` に入ったまま到着しない世帯の割合。
- 大都市で観光客の全走査をどの周期で行えば負荷が許容範囲に収まるか。
