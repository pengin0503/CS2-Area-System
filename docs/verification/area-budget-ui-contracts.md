# Area budget UI contracts (1.6.2f1)

This is the source of truth for how the Area System appears on the vanilla Budget page (design §15, plan Task 9). Every path, export and prop below was read in the supplied 1.6.2f1 UI bundle (`index.js`, module registry `Q`) and in the decompiled `Game.dll` (`BudgetUISystem`, `CityServiceBudgetSystem`). Nothing here has been verified in game; the runtime checks are listed at the end and in `area-policy-system-runtime.md`.

## Target presentation

| Page section | Vanilla group | Area row | Value |
|---|---|---|---|
| Expenses | Service Upkeep | **Area Management** | Area policy upkeep + Park management, as settled |
| Income | Service Fees | **Area Service Fees** | Park admission, as settled |

Both values come from the binding `cs2AreaSystem.areaBudget` = `{areaManagement, areaServiceFees}`, written by `AreaBudgetBindingSystem` from `AreaBudgetViewModel.From(AreaFinanceRuntime, frame)`:

- `areaManagement` is the policy upkeep (`AreaFinanceKind.PolicyUpkeep`) plus the Park management expense (`ParkManagement`) that `AreaFinanceSystem` actually applied to `PlayerMoney`.
- `areaServiceFees` is the Park admission (`ParkAdmission`) that households actually paid to the city (`AreaFinanceHouseholdTransfer`, recorded once through `RecordSettledIncome`).
- Both cover the trailing in-game day (`AreaFinanceAccounts`). That is the unit of the vanilla budget values: `BudgetApplySystem` applies each value over 262144 frames (`kUpdatesPerDay = 1024` updates, 256 frames apart), and the page labels that "monthly". Amounts of one frame and kind share one record, so the account is bounded by frames, not by visits. The window is runtime-only, so after a load it starts empty and fills during the first day. Nothing is extrapolated.
- Queued or in-flight requests are never shown. A credit cut by the native money cap shows only the credited part.

The binding system only reads. It never moves money and never invents an amount.

## Vanilla Budget UI (1.6.2f1)

### Data

`Game.UI.InGame.BudgetUISystem` (UIUpdate) binds group `budget`:

| Binding | Value |
|---|---|
| `totalIncome` | `CityServiceBudgetSystem.GetTotalIncome()` (positive) |
| `totalExpenses` | `CityServiceBudgetSystem.GetTotalExpenses()` (negative: −Σ expenses) |
| `incomeItems` / `expenseItems` | One `Game.UI.InGame.BudgetItem` per `UIEconomyConfigurationPrefab` item: `{id, color, icon, active, sources:[{id, index}]}`. `id` is the prefab's `m_ID`. Each source `id` is `Enum.GetName(IncomeSource / ExpenseSource)` and `index` is the enum value. These two bindings are written once and not updated in `OnUpdate`. |
| `incomeValues` | 14 values, `GetIncome((IncomeSource)i)` |
| `expenseValues` | 15 values, `-GetExpense((ExpenseSource)i)` (negative) |

Notes on the data:

- The item ids and the grouping of sources into items are asset data in `UIEconomyConfigurationPrefab`, which is not in `Game.dll`.
- The only ids visible in code are the activation keys `"Government"`, `"Loan Interest"` and `"Tile Upkeep"` (`BudgetUISystem`), plus `"Tile Upkeep"` in the map-tile panel. They show the ids are human-readable names.
- `ExpenseSource.ServiceUpkeep` (5) is the estimated service-building upkeep. The Area expense is not part of it, because Task 4 applies it to `PlayerMoney` directly (`area-finance-contracts.md`). The page must therefore add it once.
- `IncomeSource` is a fixed enum with no generic service-fee member. The Area income has no vanilla source, and none is borrowed.

`getItemValue(item, values)` (`game-ui/game/data-binding/economy/budget-bindings.ts`) returns `Σ values[source.index]` over `item.sources`.

### Components and extension points

All five are registered with `Q.add` and have setters, so `registry.extend(path, name, fn)` replaces the module-local variable that the bundle renders.

| Module path | Export | Used by | Props / role |
|---|---|---|---|
| `game-ui/game/components/economy-panel/budget-page/budget-page.tsx` | `BudgetPage` | `(0,z.jsx)(BudgetPage,{})` in the economy panel | No props. Reads the six `budget` bindings itself. Renders, in order: `BudgetChart`, an income `BudgetSection`, an expense `BudgetSection`, the balance `NumberField`, and `BudgetItemDetail`. |
| `.../budget-page/budget-section/budget-section.tsx` | `BudgetSection` | `BudgetPage` (twice) | `React.memo` component. Props: `{title, total, items, values, className, onMouseEnter, onMouseLeave, uiTag, selectedItem, onItemSelected}`. The header is `NumberField(total)`. There is one row per `item.active`, and its value is `getItemValue(item, values)`. |
| `.../budget-page/budget-item-detail/budget-item-detail.tsx` | `BudgetItemDetail` | `BudgetPage` | Props `{item, values, className}`. There is one row per `item.sources`, labelled `EconomyPanel.BUDGET_SUB_ITEM[<source.id>]`, with the value `values[source.index]`. `BudgetPage` passes the income values when the selected item is an income item, otherwise the expense values. |
| `game-ui/game/components/economy-panel/components/money-field/money-field.tsx` | `NumberField` | Section headers, rows, the balance and other economy pages | Props `{value, importance, className}` |
| `.../budget-page/budget-page.module.scss` | `classes` | `BudgetPage` | `classes.totalField` (`total-field_zjM`) is the class of the balance `NumberField`, and it is used nowhere else. |

How the page computes its numbers:

- Income subtotal: `totalIncome`.
- Expense subtotal: `totalExpenses`.
- Balance (`EconomyPanel.MONTHLY_BALANCE`): `totalIncome + totalExpenses`.
- Rows: `getItemValue`.
- Detail rows: `values[source.index]`.

`EconomyPanel.BUDGET_SUB_ITEM` is a hashed localized string, with key `` `EconomyPanel.BUDGET_SUB_ITEM[${hash}]` ``.

## Area extension (`UI/src/AreaBudgetDetails.tsx`)

### Registration

`registerAreaBudget(registry)` first looks up all five exports with `getModule`:

- each component must be a function or a memo object;
- `classes.totalField` must be a non-empty string.

If any lookup fails, nothing is extended and one console warning is written. The vanilla page is then fully unchanged (fail closed). There is no substitute panel and no other placement.

If every lookup succeeds, it extends `BudgetSection`, `BudgetItemDetail` and `NumberField`, and then `BudgetPage` last. Only the page wrapper provides the context that enables the other three, so if any `extend` fails part-way, the wrappers already in place pass everything through and the page stays vanilla.

### Groups

The page wrapper reads `cs2AreaSystem.areaBudget`, `budget.incomeItems` and `budget.expenseItems`, resolves the two target groups once, and provides them as React context to its own subtree only.

- **Service Upkeep:** the one expense item whose sources include `ServiceUpkeep`. If none or several match, there is no expense target.
- **Service Fees:**
  1. The one income item with id `Service Fees` that has at least one `Fee*` source.
  2. Otherwise, if exactly one income item has only `Fee*` sources, that item, provided it has at least two of them.
  3. Otherwise, no income target.

  The "two or more" rule keeps a lone parking or transport fee item from ever being taken for the Service Fees group. No single fee source (`FeeParking`, `FeePublicTransport`, …) is named or reused.
- A missing `areaBudget` value means no target on either side.

### Reconciliation

Each Area amount is added exactly once per displayed figure:

| Figure | Change |
|---|---|
| Service Upkeep row | The target item gets an extra source `{id:'CS2AS.AreaManagement', index: values.length}`, and `-areaManagement` is appended to a copy of the values. `getItemValue` then includes it once. |
| Expense subtotal | `total - areaManagement` |
| Service Fees row | Source `{id:'CS2AS.AreaServiceFees', index: values.length}`, value `+areaServiceFees` |
| Income subtotal | `total + areaServiceFees` |
| Balance (`NumberField` with `classes.totalField` inside the page) | `+ areaServiceFees` if there is an income target, `- areaManagement` if there is an expense target |
| Detail (selected target item) | The same extra source and value. It shows as **Area Management** / **Area Service Fees** through `EconomyPanel.BUDGET_SUB_ITEM[CS2AS.AreaManagement]` / `[CS2AS.AreaServiceFees]` (`NativeAreaLocalization`). |

How the reconciliation stays safe:

- Every change is computed from the incoming props on each render, never accumulated, and never written back into the binding data.
- Outside the page's subtree, the wrappers pass props through unchanged.
- A side whose group was not found is left exactly vanilla.
- An exception in the Area computation falls back to the vanilla props.

Japanese labels: エリア管理 / エリアサービス料金.

### Not reconciled: the budget chart

`BudgetChart` (`.../budget-chart/budget-chart.tsx`) reads the `budget` bindings itself and memoizes its bars on those bindings only. It takes no item or value props that could carry the Area rows.

- Making it include them would mean overriding the shared `getItemValue` helper with page-global state, or remounting the chart whenever an Area amount changes.
- The first is a global side channel. The second restarts the chart animation several times per second while settlements run.

The chart therefore stays vanilla. Its Service Upkeep and Service Fees bars exclude the Area amounts, while the rows, subtotals and balance next to it include them. This is a known presentation limitation.

## Runtime checks (not run; in-game verification needed)

1. The vanilla item ids are as assumed:
   - the expense group carrying `ServiceUpkeep` is unique;
   - an income item `Service Fees` exists, or exactly one all-`Fee*` item does.

   If either check fails, the corresponding Area row is absent (fail closed). Read the browser console for "Area budget".
2. The Service Upkeep row and the expense subtotal include **Area Management** once. Selecting the row shows **Area Management** as its own line.
3. The Service Fees row and the income subtotal include **Area Service Fees** once. Selecting the row shows **Area Service Fees** as its own line. The `FeeParking` and `FeePublicTransport` lines are unchanged.
4. The monthly balance equals the income subtotal plus the expense subtotal as displayed.
5. Other economy pages that use `NumberField` are unchanged.
