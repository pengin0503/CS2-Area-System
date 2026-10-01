# Area finance: 1.6.2f1 accounting contracts

Source: the supplied 1.6.2f1 `Game.dll` (`CS2-All-Managed-1.6.2f1-fix.zip`), decompiled with ilspycmd 9.1. Every member below was read in that decompile. `AreaFinanceApiContractTests` pins the shape of each member the adapter uses. Shape alone is not proof of meaning, so each entry also names the vanilla consumer and the units.

Nothing here has been checked in a running game. The runtime checks are listed at the end.

## Vanilla money path

| Member | Shape (1.6.2f1) | Meaning, consumer, units |
|---|---|---|
| `Game.City.PlayerMoney` | `private int m_Money`, `public bool m_Unlimited`, `public int money { get; }`, `public void Add(int)`, `const int kMaxMoney = 2000000000`, `ISerializable` | The city balance, stored on the `CitySystem.City` entity and saved with the city.<br>`Add` computes `math.clamp(m_Money + value, -kMaxMoney, kMaxMoney)`. The `int` addition happens before the clamp, so one large `value` can wrap.<br>`money` returns `kMaxMoney` while `m_Unlimited` is set. Even then, `Add` still changes the stored `m_Money`.<br>The only lower bound is `-kMaxMoney`. Vanilla spending never stops at zero. |
| `Game.Simulation.BudgetApplySystem` | `UpdateAt GameSimulation`, `GetUpdateInterval = 262144 / kUpdatesPerDay`, `public static readonly int kUpdatesPerDay = 1024` | This is the only vanilla writer of the daily budget. Every 256 frames it:<br>1. sums `CityServiceBudgetSystem.GetExpense(ExpenseSource, array)` for the 15 expense sources and `GetIncome(IncomeSource, array)` for the 14 income sources;<br>2. enqueues one `StatisticsEvent{Expense or Income, m_Change = abs(daily / 1024f), m_Parameter = source}` per source;<br>3. applies one net `PlayerMoney.Add(net / 1024)` in a job, through `ComponentLookup<PlayerMoney>` (read-write).<br>All amounts are money per day; each update applies 1/1024 of a day. |
| `Game.Simulation.CityServiceBudgetSystem` | `UpdateAfter ModificationEnd`; `GetExpense(ExpenseSource)`, `GetIncome(IncomeSource)`, `GetTotalExpenses()`, `GetTotalIncome()`, and the static array overloads | Estimates daily budget values.<br>`ExpenseSource.ServiceUpkeep` is the sum, over service prefab entities that carry `CollectedCityServiceBudgetData` and `CollectedCityServiceUpkeepData`, of `GetEstimatedServiceUpkeep`. That function adds `m_BaseCost` to each upkeep `m_FullCost`; money upkeep is first scaled by the service budget percentage.<br>`BudgetUISystem` (UIUpdate) reads `GetTotalIncome`, `GetTotalExpenses`, `GetIncome(i)` and `-GetExpense(i)` for its bindings. |
| `Game.Simulation.CityServiceUpkeepSystem` | `UpdateAt GameSimulation` | Collects building upkeep into the budget data above. Its only use of `PlayerMoney` is a discarded read; it never writes the balance.<br>Vanilla building upkeep therefore reaches city money only through `BudgetApplySystem`. |
| `Game.City.StatisticsEvent` | `StatisticType m_Statistic`, `int m_Parameter`, `float m_Change` | One statistics sample delta. |
| `Game.Simulation.CityStatisticsSystem` | `UpdateAt GameSimulation`, interval 8192 | Consumes queued statistics events.<br>`GetStatisticsEventQueue(out JobHandle)` asserts that the system is enabled. `GetSafeStatisticsQueue(out JobHandle)` returns a `SafeStatisticQueue` whose `Enqueue` drops events while the system is disabled. `AddWriter(JobHandle)` registers the writer job.<br>`ProcessStatisticsJob` adds `m_Change` to the current sample of a known `(type, parameter)` key and ignores unknown keys. |
| `Game.City.ExpenseSource` | `ServiceUpkeep = 5`, `Count = 15` | A fixed enum. Area System adds no values to it. |
| `Game.City.IncomeSource` | `TaxResidential … FeeWater`, `Count = 14` | A fixed enum with **no generic `ServiceFee` member**. Every `Fee*` value names a specific vanilla service. |
| `Game.City.PlayerResource` | `Electricity … Parking`, `Count` | A fixed enum with **no Park or admission resource**. |
| `Game.Economy.EconomyUtils.GetIncomeSource(PlayerResource)` | switch | Maps:<br>- Electricity → `FeeElectricity`<br>- Healthcare → `FeeHealthcare`<br>- the three education resources → `FeeEducation`<br>- Water and Sewage → `FeeWater`<br>- PublicTransport → `FeePublicTransport`<br>- Parking → `FeeParking`<br>- Garbage → `FeeGarbage`<br>**Every other resource, including Mail, FireResponse, Police and Count, returns `IncomeSource.Count`.** No mapping exists for Park admission. |
| `Game.Simulation.ServiceFeeSystem` | `GetServiceFees(PlayerResource)`, `GetServiceFeeIncomeEstimate(PlayerResource, float)` | Fee income for `CollectedCityServiceFeeData` (`int m_PlayerResource`, export/import/internal amounts and counts). Keyed by `PlayerResource` only. |
| `Game.Prefabs.ServiceData` | `CityService m_Service`, `bool m_BudgetAdjustable` | A service prefab belongs to one fixed `Game.City.CityService` value. |
| `Game.Simulation.CollectedCityServiceUpkeepData` / `CollectedCityServiceBudgetData` | `m_Resource`, `m_FullCost`, `m_Amount`, `m_Cost` / `m_Workplaces`, `m_Count`, `m_Export`, `m_BaseCost`, `m_Wages`, `m_FullWages` | Per-service accumulators, rebuilt by vanilla jobs and serialized with the prefab entities. |
| `EconomyUtils.GetResources(Resource, DynamicBuffer<Resources>)` / `AddResources(Resource, int, DynamicBuffer<Resources>)` | `int` / `int` | A household's money is its `Resource.Money` entry. `AddResources` clamps through `long`. |

## Expense route (Area Management)

A synthetic service (a new service prefab feeding `CollectedCityServiceBudgetData`) was rejected for four reasons:
- It needs a `CityService` value, and that enum is fixed.
- The accumulators are rebuilt and cleared by vanilla jobs.
- The components are serialized with the prefab entities.
- The service would also appear in the vanilla service budget lists.

That leaves no safe public join point in 1.6.2f1.

The chosen route is the narrow, version-pinned adapter `AreaFinanceVanillaAdapter`:
1. `AreaFinanceSystem` is registered `UpdateAfter` GameSimulation, so it runs after every `UpdateAt` producer, including `BudgetApplySystem`, and on the same 256-frame cadence.
2. It schedules one job that applies the batch's net amount to the city `PlayerMoney` through `ComponentLookup<PlayerMoney>`, exactly as `BudgetApplySystem` does.
3. The same job enqueues `StatisticsEvent{Expense, ServiceUpkeep, applied}` through `GetSafeStatisticsQueue`/`AddWriter`. Applied expense therefore enters the same service-upkeep statistic as vanilla building upkeep, in the same money units.
4. The budget page's Service Upkeep subtotal and its totals come from `CityServiceBudgetSystem` estimates, not from this statistic. Task 9 reconciles them once, with the settled amount (`AreaFinanceSystem.AreaManagementSettled`).

No unrelated `ExpenseSource` is used. Vanilla building upkeep is never an input, so nothing is booked twice.

A negative city balance does not shorten an expense. Only the native `±kMaxMoney` clamp can, and then only the part that `PlayerMoney` cannot represent. In that case `Apply` reports the expense that was actually applied, and the change in the stored balance always equals `Income - Expense`.

## Income route (Area Service Fees)

Park admission cannot use `CollectedCityServiceFeeData` or any `IncomeSource`. No `PlayerResource` represents it, `GetIncomeSource` returns `IncomeSource.Count` for unsupported resources, and every existing `Fee*` source would mislabel it.

- **Household payments** go through `AreaFinanceHouseholdTransfer.Transfer` inside the payer's job. The amount is the smallest of the fee, the household's money and the city's headroom below `kMaxMoney`. The city is credited first. The household is then debited by exactly the credited amount, so a zero credit means no debit. The owner records the result once with `AreaFinanceSystem.RecordSettledIncome`, which never credits the city again.
- **Payer-less Area income** (none exists yet) goes through `EnqueueIncome` and the same job as expenses.
- **No income statistics event** is written, because there is no `IncomeSource` key for it. Writing one under an unrelated key would mislabel it. Task 9 presents the settled amount (`AreaFinanceSystem.AreaServiceFeesSettled`) as the Area Service Fees row inside the vanilla Service Fees group.

## Order and save boundary

- Producers run in GameSimulation (`UpdateAt`) and queue settled amounts with `EnqueueExpense`/`EnqueueIncome`. `AreaFinanceSystem` (`UpdateAfter`) takes the batch and schedules the job. The results are recorded on its next update, and the accounts hold only applied amounts.
- `AreaFinanceSaveSystem` runs `UpdateBefore<SerializerSystem>` in Serialize. It completes the job in flight, completes tracked jobs, applies anything still queued and records it, so `PlayerMoney` is never serialized with Area amounts still pending. The logic is `AreaFinanceSaveBoundary.Flush`.
- `OnGamePreload` clears the pending requests and the settled history. Nothing is back-charged across a load.
- `AreaFinanceAccounts` keeps the trailing in-game day of settled amounts. It is runtime-only: after a load it starts empty, and `SettledCoveredFrames` reports how much of the day it has observed so far.

## Rounding

`AreaFinanceModel.SettleDaily(perDay, frames, ref carry)` computes `perDay × frames / 262144 + carry`. It floors that value with a 1e-9 tolerance and carries the fraction to the next settlement.
- An invalid rate (NaN, infinite or negative) settles nothing.
- A carry outside [0, 1) is reset and never becomes money.
- Results saturate at `long.MaxValue`.

## Requires a 1.6.2f1 runtime check

These checks are listed in `docs/verification/area-policy-system-runtime.md`:
- The city balance drops by the applied Area Management amount, including when it is already negative.
- The Expense/ServiceUpkeep statistic, as shown in the city statistics charts, includes the Area amount. This depends on a `StatisticsData` prefab existing for that key, which is asset data and cannot be read from `Game.dll`.
- Park admission household debit and city credit are equal.
- A save made between scheduling and recording reloads with matching counters and balance.
