# Area Policy System — 1.6.2f1 runtime verification record

**Status: NOT RUN.** No item below has been executed in the game. There is no runtime observation yet, so no row is marked PASS or FAIL.

| | |
|---|---|
| Target game version | Cities: Skylines II 1.6.2f1 (the supplied `CS2-All-Managed-1.6.2f1-fix` DLLs, UI bundle and toolchain zips) |
| Build used for runtime | None. The official-toolchain build (`dotnet build` with the CS2 toolchain environment, net48 target, deploy to the Mods folder) could not be run in the implementation environment. That environment has the API-check build only (net8 against the supplied DLLs), which is not a playable mod. |
| Automated evidence | `scripts/verify.ps1 -ApiCheck`: source guards, offline contracts, NUnit, C# API-check build, Vitest and the UI production build. None of these establishes an item below. |
| Finance adapter mode | Fallback accounting boundary (`docs/verification/area-finance-contracts.md`). Area expense and payer-less income are applied to the city `PlayerMoney` by `AreaFinanceSystem` at `BudgetApplySystem`'s cadence, and the expense is also written to the `Expense/ServiceUpkeep` statistic. Park admission is a household-to-city transfer. The Budget page shows both through the UI reconciliation in `docs/verification/area-budget-ui-contracts.md` (Service Upkeep → Area Management, Service Fees → Area Service Fees). No vanilla `IncomeSource` or `PlayerResource` is used for Area income. |

## Policies left EffectUnavailable on purpose

These policies are visible but cannot be enabled, are never effective and are never billed.

| ID | Policy | Reason (details in the linked contract) |
|---:|---|---|
| 2002 | Night Events | No safe visitor-pressure path: `AttractivenessProvider` is recomputed by `AttractionSystem` and feeds vanilla tourism. The noise channel `PollutionEmitModifier` would leave save residue and is not shown in the vanilla UI (`park-policy-effect-contracts.md`). |
| 2003 | Quiet Operations | Same noise-channel constraints as 2002; the visitor-pressure half has no path. |
| 2006 | Major Event Attraction | Same attractiveness constraints as 2002, and it would affect vanilla tourism. |
| 3001 | Enhanced Safety Audits | Fire hazard is reachable only through vanilla District modifiers. Areas are not Districts (`industrial-policy-effect-contracts.md`). |
| 3002 | Stronger Environmental Regulation | A pollution channel exists, but the required production trade-off has none. |
| 3003 | Production Priority | No production/efficiency channel a mod can own. |
| 3004 | Logistics Priority | No per-building logistics priority path. |
| 3005 | 24-Hour Operations | Shifts are shared prefab data. There is no per-company operating-hours state. |
| 3006 | Restricted Operating Hours | A noise channel exists, but the activity trade-off has none. |
| 3007 | Advanced Automation | Workers are driven by `IndustrialAISystem`. There is no productivity channel. |

Effective policies:

| ID | Policy | Effect |
|---:|---|---|
| 1001 | Focused Management | XP multiplier for Park and Industrial |
| 2001 | Enhanced Maintenance | Park management funding ×1.25 |
| 2004 | Landscape Protection | Scenic credit ×1.25, comfortable capacity ×0.85 |
| 2005 | Premium Services | Fair fee ×1.3 |

## Runtime checklist (plan Task 9, Step 6)

Record the game version and build, save, city size and the observation for each item. Do not commit saves.

| # | Check | Result |
|---:|---|---|
| 1 | The policy section looks and behaves like the vanilla District policies section (vanilla `Policy`/`CompactPolicy` and foldout; tooltips; gamepad focus). It is not a floating window. | NOT RUN |
| 2 | Two Areas of the same type keep independent policy states through toggles and save/load. | NOT RUN |
| 3 | Enabling a policy whose conflict partner is on swaps them in one action. No frame shows both on, and no upkeep is billed for both. | NOT RUN |
| 4 | A policy above the Area's Level shows the Level requirement and cannot be enabled. After a level-up it becomes available and starts OFF. | NOT RUN |
| 5 | Suspending an Area (remove its entrance or HQ) keeps the desired states, while effects and upkeep stop. Re-activating restores the same states without manual re-entry. | NOT RUN |
| 6 | The Budget page's Service Upkeep row and expense subtotal include Area Management exactly once, and its detail shows an **Area Management** line. | NOT RUN |
| 7 | The Budget page's Service Fees row and income subtotal include Area Service Fees exactly once, backed by real admission credits. Its detail shows an **Area Service Fees** line, and the `FeeParking`/`FeePublicTransport` lines are unchanged. | NOT RUN |
| 8 | The displayed income/expense totals and the balance reconcile with the city-money change and the Area rows over a day. The budget chart is known not to include the Area amounts (`area-budget-ui-contracts.md`). | NOT RUN |
| 9 | With a negative city balance, Area Management (policy upkeep + Park management) is still charged in full. | NOT RUN |
| 10 | Park member buildings' vanilla upkeep is not charged again by the Area System. Compare the vanilla Service Upkeep with and without a Park Area. | NOT RUN |
| 11 | For one paid Park visit: the household debit equals the city credit, and that amount appears in Area Service Fees. | NOT RUN |
| 12 | Each enabled policy changes its documented target: 1001 XP rate; 2001 management cost and Maintenance; 2004 Landscape rows and capacity; 2005 value for money. | NOT RUN |
| 13 | Industrial scores move only through observation. No Industrial policy can be enabled. | NOT RUN |
| 14 | Save/load restores desired and effective states, Level locks and finance totals, without a back-charge or duplicate charge after load. | NOT RUN |
| 15 | The unverified policies above are visibly unavailable and add no Policy Upkeep. | NOT RUN |

Further runtime checks are listed in the contract documents: `area-policy-ui-contracts.md`, `area-finance-contracts.md`, `area-budget-ui-contracts.md`, `park-policy-effect-contracts.md` and `industrial-policy-effect-contracts.md`.

Open runtime issues #30, #38, #43 and #44 were not reproduced in this work and remain open.
