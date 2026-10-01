# Area Policy / Ordinance System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Execution target for this project:** Opus 5.5 in Native mode. Use `superpowers:executing-plans` and execute tasks sequentially unless the user explicitly changes the method.

**Goal:** Add per-Area vanilla-style policies/ordinances, shared vanilla-aligned Area finance, Park finance migration, and verified Park/Industrial policy effects without breaking the observation-first Area architecture.

**Architecture:** Area Core owns policy identity, per-Area desired state, unlock/conflict/effective-state resolution, persistence, UI bindings, and shared finance contracts. Park and Industrial own their policy definitions and effects; vanilla-facing effects sit behind version-sensitive adapters and remain unavailable when their 1.6.2f1 path cannot be proven. Finance uses one shared Area accounting layer and real city transactions, with UI detail rows backed by the same settled amounts.

**Tech Stack:** C# 9, Unity ECS / Cities: Skylines II 1.6.2f1 managed APIs, Colossal UI bindings, React 18, TypeScript 5.7, Vitest, NUnit 4, official CS2 modding toolchain.

**Spec:** `docs/superpowers/specs/2026-10-01-area-policy-system-design.md`

## Global Constraints

- Target game version is exactly Cities: Skylines II **1.6.2f1** for all version-sensitive API contracts.
- Use the supplied `CS2-All-Managed-1.6.2f1-fix.zip`, `CS2-toolchain-1.6.2f1.zip`, and `CS2-UI-reference-1.6.2f1.zip` when verifying game/UI contracts; do not guess members or UI module paths.
- Preserve the dependency rule: Area Core must not contain Park- or Industrial-specific effect logic.
- Policies are configured independently per Area instance.
- Policy IDs are fixed: common 1000–1999, Park 2000–2999, Industrial 3000–3999. Never reuse a released ID.
- No simultaneous-policy slot cap.
- Explicit opposing policies are mutually exclusive; enabling one atomically disables conflicting policies in the same Area.
- All newly unlocked policies default OFF.
- Every effective policy has positive upkeep.
- Suspended Areas retain desired policy state, but effects and upkeep pause.
- Policy ON/OFF takes effect immediately at the next relevant simulation update; finance settles from the same simulation-time boundary.
- No Area wallet, no Area debt ledger, no “insufficient city funds” suppression. Negative city balance does not stop Area expenses.
- Expenses must feed real city accounting and target **Service Upkeep → Area Management**.
- Income must feed real city money and target **Service Fees → Area Service Fees** in the budget UI; 1.6.2f1 has no generic `IncomeSource.ServiceFee`, so Park admission must not be mislabeled as `FeeParking`, `FeePublicTransport`, or any unrelated fixed source.
- `CollectedCityServiceFeeData` / `PlayerResource` may be used only when an Area charge genuinely maps to an existing vanilla resource; Park admission does not.
- Never double-book vanilla building upkeep or vanilla service fees.
- Vanilla-simulation policies must not directly manipulate Industrial/Park scores as a substitute for changing the real underlying state.
- If a vanilla effect path cannot be verified, keep that policy visible but effect-unavailable/disabled; do not fabricate a substitute effect.
- Existing Park/Industrial panel cleanup is out of scope.
- Tourism policies are out of scope.
- Keep Harmony out of the baseline implementation. A version-pinned adapter must first prefer public prefab/ECS/UI extension points already used by this project.
- Preserve the existing failure-isolation pattern: one adapter/UI/module failure must not disable Area Core or unrelated Areas.
- Keep source files focused; do not turn `AreaManagementUISystem.cs`, `ParkFinanceSystem.cs`, or `Mod.cs` into policy/finance dumping grounds.
- Full verification command after each integration-sized task: `powershell -ExecutionPolicy Bypass -File scripts/verify.ps1 -ApiCheck`.
- Before playable runtime verification, also run the normal toolchain build/deploy with the user's existing CS2 environment, then follow `scripts/runtime-smoke-checklist.md`.

## Review Focus

1. **Unknown saved PolicyId after a module is removed:** the raw ID and desired state must survive save/load, produce no effect/upkeep, and reactivate if the definition returns. Task 1 adds the round-trip test.
2. **Atomic conflict swap near a finance settlement boundary:** old and new policies must never overlap for effect or billing and must not create a zero-length double charge. Tasks 2, 4, and 6 add transition/settlement tests.
3. **City balance already negative:** Area expenses must still settle and appear in real Service Upkeep totals; no “paid amount” clamp may survive from Park finance. Tasks 4–5 add model/API/runtime checks.
4. **Vanilla effect adapter unavailable after a game/API change:** desired state is preserved, the policy becomes effect-unavailable and non-billable, other policies/Areas continue, and repeated logs are suppressed per Area+Policy+failure class per load generation. Tasks 2, 7, and 8 add tests.
5. **Park admission at save/accounting boundaries:** household debit and city credit must not diverge after reload, and the amount must remain Area Service Fees rather than being classified as Parking/Public Transport/etc. Tasks 5 and 9 own the transaction and presentation regressions.

---

### Task 1: Policy Domain, Registry, and Per-Area Persistence

**Files:**
- Create: `src/CS2AreaSystem/Core/Policies/AreaPolicyIds.cs`
- Create: `src/CS2AreaSystem/Core/Policies/AreaPolicyDefinition.cs`
- Create: `src/CS2AreaSystem/Core/Policies/AreaPolicyUpkeep.cs`
- Create: `src/CS2AreaSystem/Core/Policies/AreaPolicyRegistry.cs`
- Create: `src/CS2AreaSystem/Core/Policies/ManagedAreaPolicyEntry.cs`
- Create: `src/CS2AreaSystem/Core/Policies/AreaPolicyTransition.cs`
- Create: `src/CS2AreaSystem/Core/Policies/AreaPolicyModel.cs`
- Create: `src/CS2AreaSystem/Core/Policies/AreaPolicyBootstrapSystem.cs`
- Create: `src/CS2AreaSystem/Core/Modules/IAreaPolicyProvider.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaPolicyRegistryTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaPolicyModelTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaPolicySerializationTests.cs`
- Modify: `tests/CS2AreaSystem.Tests/ModSystemOrderTests.cs`

**Interfaces:**
- Produces `AreaPolicyIds` constants:
  - `FocusedManagement = 1001`
  - `EnhancedMaintenance = 2001`
  - `NightEvents = 2002`
  - `QuietOperations = 2003`
  - `LandscapeProtection = 2004`
  - `PremiumServices = 2005`
  - `MajorEventAttraction = 2006`
  - `EnhancedSafetyAudits = 3001`
  - `EnvironmentalRegulation = 3002`
  - `ProductionPriority = 3003`
  - `LogisticsPriority = 3004`
  - `TwentyFourHourOperations = 3005`
  - `RestrictedOperatingHours = 3006`
  - `AdvancedAutomation = 3007`
- Produces `enum AreaPolicyEffectClass : byte { AreaInternal, VanillaSimulation }`.
- Produces `sealed class AreaPolicyDefinition` with immutable properties: `PolicyId`, `NameKey`, `DescriptionKey`, `IconUri`, `IReadOnlyCollection<int> AreaTypeIds`, `MinLevel`, `AreaPolicyUpkeep Upkeep`, `string ConflictGroup`, `AreaPolicyEffectClass EffectClass`, `string EffectHandlerKey`.
- Produces `readonly struct AreaPolicyUpkeep` with `double BaseDailyUpkeep, PerHectare, PerEligibleFacility, PerActivityUnit` and `string ActivityBasis`.
- Produces `struct ManagedAreaPolicyEntry : IBufferElementData, ISerializable` with `int PolicyId`, `byte DesiredEnabled`, schema version, and `bool IsDesiredEnabled`.
- Produces `AreaPolicyRegistry.Register(AreaPolicyDefinition)`, `TryGet(int, out AreaPolicyDefinition)`, and `ForAreaType(int)`.
- Produces pure `AreaPolicyModel.EvaluateToggle(AreaPolicyRegistry registry, int areaTypeId, int level, IReadOnlyCollection<int> desiredEnabledIds, int policyId, bool enable) -> AreaPolicyTransition`.
- `AreaPolicyTransition` contains `Accepted`, `ReasonKey`, `PolicyId`, `DesiredEnabled`, and `IReadOnlyList<int> DisablePolicyIds`.
- `IAreaPolicyProvider` exposes `IReadOnlyList<AreaPolicyDefinition> PolicyDefinitions { get; }`.
- `AreaPolicyBootstrapSystem` adds an empty `ManagedAreaPolicyEntry` buffer only to managed Areas that do not already have one; it never replaces an existing buffer.

- [ ] **Step 1: Write registry/model/persistence failing tests**

Assert exact IDs, duplicate registration rejection, Area-type filtering, Level locks, OFF-by-default semantics, conflict-disable lists, unrelated policies remaining enabled, unknown PolicyId rejection for a new toggle, and unknown serialized entries round-tripping unchanged.

- [ ] **Step 2: Run the focused tests and verify RED**

Run:
`dotnet test tests/CS2AreaSystem.Tests/CS2AreaSystem.Tests.csproj -c Debug -p:AreaApiCheck=true --filter "AreaPolicyRegistryTests|AreaPolicyModelTests|AreaPolicySerializationTests"`

Expected: FAIL because the policy domain types do not exist.

- [ ] **Step 3: Implement the policy domain and serialization types**

Use `ComponentSchema.RequireSupported` consistently with existing Area components. Serialize the buffer element as schema version → PolicyId → DesiredEnabled byte. Unknown IDs are data, not an error.

- [ ] **Step 4: Implement `AreaPolicyBootstrapSystem` and register it**

Register after `AreaLoadCoordinatorSystem` in `ModificationEnd`. Add/extend schedule tests so a loaded serialized buffer exists before later policy/UI/finance consumers run.

- [ ] **Step 5: Re-run focused tests and verify GREEN**

Expected: all Task 1 tests PASS.

- [ ] **Step 6: Commit**

`git commit -am "feat: add persisted area policy domain"` plus newly created files.

---

### Task 2: Policy Runtime, Module Providers, Conflict/Effective-State Resolution

**Files:**
- Create: `src/CS2AreaSystem/Core/Policies/AreaPolicyContext.cs`
- Create: `src/CS2AreaSystem/Core/Policies/AreaPolicyRuntimeState.cs`
- Create: `src/CS2AreaSystem/Core/Policies/AreaPolicySystem.cs`
- Create: `src/CS2AreaSystem/Core/Policies/CommonAreaPolicies.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkPolicyCatalog.cs`
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialPolicyCatalog.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkAreaModule.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialAreaModule.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaPolicyRuntimeTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaPolicyCatalogTests.cs`
- Modify: `tests/CS2AreaSystem.Tests/ModSystemOrderTests.cs`

**Interfaces:**
- `AreaPolicyContext(World world, EntityManager entities)`.
- Extend optional module policy contract with:
  - `bool IsPolicyEffectAvailable(Entity area, int policyId, in AreaPolicyContext context, out string reasonKey)`.
- `AreaPolicyRuntimeState` fields: `Unlocked`, `DesiredEnabled`, `EffectAvailable`, `Suspended`, `Effective`, `ReasonKey`.
- `AreaPolicySystem.Registry` is authoritative for registered definitions.
- `AreaPolicySystem.TrySetDesired(Entity area, int policyId, bool enabled, out string reasonKey) -> bool`.
- `AreaPolicySystem.GetState(Entity area, int policyId) -> AreaPolicyRuntimeState`.
- `AreaPolicySystem.IsEffective(Entity area, int policyId) -> bool`.
- `AreaPolicySystem.GetExperienceMultiplier(Entity area) -> double`, returning exactly `1.10` only when Focused Management is effective, otherwise `1.0`.
- Common Focused Management supports Park and Industrial only in this release.
- Conflict groups are fixed strings:
  - `park.activity` for Night Events / Quiet Operations
  - `industrial.environment-production` for Environmental Regulation / Production Priority
  - `industrial.hours` for 24-Hour / Restricted Hours
- Initial catalogs contain all policy IDs and Level requirements from the spec.
- Policies whose vanilla adapters are not yet proven report `EffectAvailable=false` with a localized `EffectUnavailable` reason until their adapter task enables them.
- An effect-unavailable desired policy is not `Effective` and accrues no upkeep.

- [ ] **Step 1: Write failing runtime tests**

Cover per-Area independence, Level unlocks, atomic conflict replacement, Suspended desired state, reactivation, effect-unavailable behavior, Focused Management multiplier, and the Review Focus failure-log key `AreaStableId + PolicyId + failure class + load generation`.

- [ ] **Step 2: Run focused tests and verify RED**

Expected: missing runtime system/catalog/provider APIs.

- [ ] **Step 3: Implement catalogs and module policy providers**

Do not put Park/Industrial switch statements into Core. Park/Industrial modules expose their own definitions and availability.

- [ ] **Step 4: Implement `AreaPolicySystem`**

Build registry during `OnCreate` from common policies plus modules implementing `IAreaPolicyProvider`. Resolve desired state from the Area buffer every time; do not persist derived effective state.

- [ ] **Step 5: Register runtime system and re-run tests**

Register in `ModificationEnd` after bootstrap/load/lifecycle so current lifecycle and progression Level are available.

- [ ] **Step 6: Commit**

`git commit -am "feat: add area policy runtime and catalogs"`.

---

### Task 3: Vanilla-Style Selected-Info Policy UI and Guarded Actions

**Files:**
- Create: `src/CS2AreaSystem/Core/UI/AreaPolicyView.cs`
- Modify: `src/CS2AreaSystem/Core/UI/AreaManagementViewModel.cs`
- Modify: `src/CS2AreaSystem/Core/UI/AreaManagementUISystem.cs`
- Modify: `src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`
- Create: `UI/src/AreaPoliciesSection.tsx`
- Create: `UI/src/AreaPoliciesSection.test.tsx`
- Modify: `UI/src/AreaInfoSection.tsx`
- Modify: `UI/src/AreaInfoSection.test.tsx`
- Modify: `UI/src/bindings.ts`
- Modify: `UI/src/vanilla.tsx`
- Create: `docs/verification/area-policy-ui-contracts.md`
- Test: `tests/CS2AreaSystem.Tests/AreaPolicyViewModelTests.cs`
- Modify: `tests/CS2AreaSystem.Tests/AreaUIActionGuardTests.cs`

**Interfaces:**
- `AreaPolicyView` JSON fields: `policyId`, `nameKey`, `descriptionKey`, `icon`, `minLevel`, `desiredEnabled`, `effective`, `locked`, `suspended`, `effectAvailable`, `reasonKey`, `estimatedDailyUpkeep`.
- `AreaManagementViewModel.Policies` is a stable ordered list: common first, then module policies by PolicyId.
- Add guarded trigger `cs2AreaSystem/setPolicyEnabled(stableId:string, policyId:string, enabled:string)`; enabled is `"1"` or `"0"` so it can use the already-proven three-string TriggerBinding shape.
- C# re-validates current selected Area, stable ID, policy ID, current Level, Area type and policy availability before mutation.
- React never computes unlock/conflict/effective/upkeep state.
- `AreaPoliciesSection` receives `stableId` and `policies`, sends only the guarded trigger, and renders inside the existing selected-info section.
- Locked policies remain visible.
- Suspended + desired ON renders ON/configured plus suspended status.
- Effect-unavailable renders disabled/effect-unavailable and cannot be enabled.
- Conflict toggle is one user action; the refreshed C# state shows the old policy OFF and the new policy ON.
- Prefer actual vanilla District-policy modules. Before writing module paths/prop adapters, inspect the supplied 1.6.2f1 UI reference/runtime module registry and record the exact module path/export/props in `docs/verification/area-policy-ui-contracts.md`. If exact vanilla component reuse is not possible, use the existing vanilla `InfoSection`/row/toggle patterns as fallback without a floating window.

- [ ] **Step 1: Add C# and React failing tests**

Assert JSON shape, stable ordering, locked/suspended/unavailable rendering, trigger arguments, stale stable-ID rejection, conflict refresh, and fallback rendering when `getModule` throws.

- [ ] **Step 2: Verify RED in C# and UI tests**

Run the targeted NUnit tests and `npm test -- --run AreaPoliciesSection.test.tsx AreaInfoSection.test.tsx`.

- [ ] **Step 3: Verify and document the 1.6.2f1 vanilla policy UI contract**

Use the supplied UI reference and/or runtime module registry. Record exact findings; do not invent a path from type names.

- [ ] **Step 4: Implement C# policy view/action plumbing**

Keep `AreaManagementUISystem` orchestration thin; put policy serialization/view construction in `AreaPolicyView` helpers rather than adding large blocks to the UI system.

- [ ] **Step 5: Implement React policy section and vanilla lookup/fallback**

No custom floating policy panel.

- [ ] **Step 6: Run C# + UI focused tests and build UI**

Expected: tests PASS, `npm run build` PASS.

- [ ] **Step 7: Commit**

`git commit -am "feat: add vanilla-style area policy UI"`.

---

### Task 4: Shared Area Finance Model and 1.6.2f1 Accounting Contract

**Files:**
- Create: `src/CS2AreaSystem/Core/Finance/AreaFinanceKind.cs`
- Create: `src/CS2AreaSystem/Core/Finance/AreaFinanceModel.cs`
- Create: `src/CS2AreaSystem/Core/Finance/AreaFinanceLedger.cs`
- Create: `src/CS2AreaSystem/Core/Finance/AreaFinanceVanillaAdapter.cs`
- Create: `src/CS2AreaSystem/Core/Finance/AreaFinanceHouseholdTransfer.cs`
- Create: `src/CS2AreaSystem/Core/Finance/AreaFinanceSystem.cs`
- Create: `src/CS2AreaSystem/Core/Finance/AreaFinanceSaveSystem.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Create: `docs/verification/area-finance-contracts.md`
- Test: `tests/CS2AreaSystem.Tests/AreaFinanceModelTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaFinanceLedgerTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaFinanceApiContractTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaFinanceVanillaAdapterTests.cs`
- Modify: `tests/CS2AreaSystem.Tests/ModSystemOrderTests.cs`

**Interfaces:**
- `enum AreaFinanceKind : byte { PolicyUpkeep, ParkManagement, ParkAdmission }`.
- `AreaFinanceModel.FramesPerDay = 262144`.
- `AreaFinanceModel.SettleDaily(double perDay, uint frames, ref double carry) -> long`: non-negative finite input only, floor settlement with fractional carry, no wall-clock use.
- `AreaFinanceLedger.EnqueueExpense(ulong areaStableId, AreaFinanceKind kind, long amount)`.
- `AreaFinanceLedger.EnqueueIncome(ulong areaStableId, AreaFinanceKind kind, long amount)`.
- `AreaFinanceLedger.TakeBatch() -> AreaFinanceBatch` aggregates without losing per-kind/per-Area audit detail.
- `AreaFinanceVanillaAdapter.ApplyExpense(long amount) -> long` returns the amount actually applied to real city money/accounting.
- `AreaFinanceVanillaAdapter.ApplyIncome(long amount) -> long` returns the amount actually credited to real city money for Area-originated income that has no external payer. It must **not** write Park admission into `CollectedCityServiceFeeData` or select any unrelated fixed `IncomeSource`.
- `AreaFinanceHouseholdTransfer.Transfer(ref PlayerMoney cityMoney, DynamicBuffer<Resources> householdResources, long requested) -> long` performs the Park-style payer-to-city transfer atomically: compute the household-payable amount, apply the city credit with native `PlayerMoney` saturation semantics, debit the household by exactly the city-applied amount, and return that exact amount. A failed/zero city credit means zero household debit.
- `AreaFinanceSystem.RecordSettledIncome(ulong areaStableId, AreaFinanceKind kind, long amount)` records an already-applied atomic transfer for budget presentation/lifetime accounting without crediting city money a second time.
- `AreaFinanceSystem.EnqueueExpense(...)`, `EnqueueIncome(...)`, `RecordSettledIncome(...)`, `AreaFinanceHouseholdTransfer.Transfer(...)`, and `FlushForSave()` are the only Core finance entry points available to Park/policy code after Task 5; Park code must not call `PlayerMoney.Add` directly.
- `AreaFinanceSystem.AreaManagementSettled` and `AreaFinanceSystem.AreaServiceFeesSettled` expose authoritative settled totals for Task 9 budget presentation. They are derived from the same batches applied to city money, never independent UI counters.
- Expense target: prefer the verified public `ExpenseSource.ServiceUpkeep`/service-upkeep accounting route. If 1.6.2f1 fixed `CityService` semantics prevent a synthetic service from joining safely, `AreaFinanceVanillaAdapter` uses the narrow version-pinned budget-accounting boundary documented in `area-finance-contracts.md`; it must still debit real city money and never repurpose an unrelated expense source.
- Income target: 1.6.2f1 has no generic `IncomeSource.ServiceFee`. The fixed fee sources are resource-specific. Park admission therefore credits real city money through the Area adapter and is presented/reconciled under the Service Fees UI group in Task 9 using `AreaServiceFeesSettled`.
- Before implementing writes, inspect the supplied 1.6.2f1 Game.dll and document exact fields/consumers for `PlayerMoney`, `ExpenseSource`, `IncomeSource`, `PlayerResource`, `ServicePrefab`, `ServiceData`, `CollectedCityServiceUpkeepData`, `CollectedCityServiceFeeData`, `CityServiceBudgetSystem`, `ServiceFeeSystem`, and the relevant upkeep systems.
- The contract document must explicitly record that `EconomyUtils.GetIncomeSource(PlayerResource)` has no Park/admission mapping and returns `IncomeSource.Count` for unsupported resources.
- Reflection/API tests assert every game member the adapter actually uses. Existence alone is not semantic proof; the document names the vanilla consumer and units/order.
- Pending finance batches are flushable before `SerializerSystem`; no save may serialize counters before the corresponding real city-money transfer is settled.

- [ ] **Step 1: Write pure finance model/ledger failing tests**

Cover fractional carry, partial-day proration, invalid/negative inputs, very large finite values, zero-length conflict transitions, and no duplicate aggregation.

- [ ] **Step 2: Inspect 1.6.2f1 finance APIs and write the contract tests/document**

Use the supplied Managed zip. Assert the fixed enum members and explicitly prove there is no generic `IncomeSource.ServiceFee` or Park `PlayerResource`. Document the exact supported expense path and the exact city-money write path for Area income.

- [ ] **Step 3: Run contract tests before the Area adapter exists and verify RED**

Expected: reflection assertions for vanilla members PASS; Area adapter tests FAIL because `AreaFinanceVanillaAdapter` is not implemented.

- [ ] **Step 4: Implement ledger, real city-money adapter, runtime settlement, and save flush**

Do not suppress Area expenses merely because the current city balance is negative; allow native `PlayerMoney` saturation semantics at the engine's representable bounds. Move Park admission saturation/accepted-delta logic into `AreaFinanceHouseholdTransfer` so household debit always equals the actual city credit. Do not write Park admission into any unrelated `IncomeSource`.

- [ ] **Step 5: Add system-order tests**

Ensure settlement runs after the producers that enqueue Area charges and before the verified vanilla consumer/save boundary required by the chosen 1.6.2f1 path; ensure `AreaFinanceSaveSystem` runs before `SerializerSystem`.

- [ ] **Step 6: Run focused tests + full API-check verification**

Expected: Task 4 tests and `scripts/verify.ps1 -ApiCheck` PASS.

- [ ] **Step 7: Commit**

`git commit -am "feat: add shared vanilla-aligned area finance"`.

---

### Task 5: Migrate Park Management and Admission City Accounting to Shared Finance

**Files:**
- Modify: `src/CS2AreaSystem/Modules/Park/ParkFinanceModel.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkFinanceSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkTransactions.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkFinanceSaveBoundary.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkAreaState.cs` only if schema changes are strictly required; prefer keeping lifetime counters compatible.
- Modify: `tests/CS2AreaSystem.Tests/ParkFinanceModelTests.cs`
- Modify: `tests/CS2AreaSystem.Tests/ParkCityMoneyTransferTests.cs`
- Modify: `tests/CS2AreaSystem.Tests/ParkTransactionTests.cs`
- Modify: `tests/CS2AreaSystem.Tests/ParkFinanceSaveBoundaryTests.cs`
- Create: `tests/CS2AreaSystem.Tests/ParkAreaFinanceIntegrationTests.cs`

**Interfaces:**
- Remove `ParkFinanceModel.PayManagement(ref PlayerMoney,...)` as the runtime management-payment path.
- Park management calculates requested settled expense exactly as before, then calls `AreaFinanceSystem.EnqueueExpense(stableId, ParkManagement, amount)`.
- Household admission debit remains real household `Resource.Money` debit.
- Replace Park's local admission money primitive with `AreaFinanceHouseholdTransfer.Transfer(...)` inside the existing scheduled transaction job. The returned amount is the single source of truth for both household debit and city credit.
- After the job returns the exact applied amount, call `AreaFinanceSystem.RecordSettledIncome(stableId, ParkAdmission, collected)`; this records finance/UI accounting only and must not credit the city again.
- `ParkAdmissionPlan.Collected` remains the pre-transfer household-payable plan amount; the actual collected outcome is the transfer helper's returned applied amount and may be lower only because the native city balance has reached its representable saturation bound.
- Lifetime Park revenue/expense counters record the amount actually applied by the shared Area finance path.
- Save flush ordering must guarantee: pending household debit result is recorded → resulting Area income request is queued → Area finance flush applies city-side accounting → Park counters serialize.
- Delete obsolete city-balance-lower-bound management tests and replace them with “negative city balance still incurs full expense” tests.

- [ ] **Step 1: Rewrite failing Park finance expectations**

Add regression for city balance below zero, household insufficient funds, admission near the native upper city-money saturation bound, exact household-debit/city-credit equality, no double credit from `RecordSettledIncome`, and save during pending transfer.

- [ ] **Step 2: Verify RED**

Expected: old PlayerMoney-clamped behavior fails the new tests.

- [ ] **Step 3: Route Park city-side money through `AreaFinanceSystem`**

Use the Core transfer helper in the Park job, then record the already-settled income. Do not double-credit city money in `AreaFinanceSystem`.

- [ ] **Step 4: Update save boundary and lifetime-counter recording**

One visit produces at most one household debit and one matching Area Service Fee income.

- [ ] **Step 5: Run all Park finance tests and full API-check verification**

Expected: PASS; no old “paid only down to -2,000,000,000” management semantics remain.

- [ ] **Step 6: Commit**

`git commit -am "refactor: move park finance into shared area accounting"`.

---

### Task 6: Policy Upkeep Settlement and Focused Management XP

**Files:**
- Create: `src/CS2AreaSystem/Core/Policies/AreaPolicyUpkeepSystem.cs`
- Create: `src/CS2AreaSystem/Core/Policies/AreaPolicyExperience.cs`
- Create: `src/CS2AreaSystem/Core/Policies/AreaPolicyUpkeepInputs.cs`
- Create: `src/CS2AreaSystem/Core/Modules/IAreaPolicyUpkeepProvider.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkProgressionModel.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkProgressionSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialProgressionModel.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialProgressionSystem.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaPolicyUpkeepTests.cs`
- Test: `tests/CS2AreaSystem.Tests/AreaPolicyExperienceTests.cs`
- Modify: `tests/CS2AreaSystem.Tests/ParkProgressionModelTests.cs`
- Modify: `tests/CS2AreaSystem.Tests/IndustrialProgressionModelTests.cs`

**Interfaces:**
- `AreaPolicyExperience.Apply(long baseAward, double multiplier, long remainingCap) -> long`; round with `MidpointRounding.AwayFromZero`, never exceed non-negative remaining cap, saturate long arithmetic.
- Focused Management multiplier is exactly `1.10`.
- Park:
  - `ParkProgressionModel.AwardVisitXp(..., double xpMultiplier)`.
  - `ParkProgressionModel.Step(..., double xpMultiplier)`.
  - passive XP applies multiplier before enforcing the existing 40/day cap.
- Industrial:
  - `IndustrialProgressionModel.ComputeHourlyXp(..., double xpMultiplier)`.
  - multiplier applies to an otherwise-qualified base award before enforcing the existing 240/day cap.
  - zero/ineligible XP remains zero.
- `AreaPolicyUpkeepInputs` fields: `double AreaSquareMeters`, `int EligibleFacilityCount`, `double ActivityUnits`.
- `IAreaPolicyUpkeepProvider.TryGetPolicyUpkeepInputs(Entity area, int policyId, in AreaPolicyContext context, out AreaPolicyUpkeepInputs inputs)` supplies only stable module-owned billing inputs. Core never knows Park/Industrial facility semantics.
- `AreaPolicyUpkeepSystem` iterates Active managed Areas with policy buffers, asks `AreaPolicySystem` for effective policies, obtains stable inputs from the owning module when a non-zero coefficient needs them, computes the spec formula, prorates by simulation frames through `AreaFinanceModel.SettleDaily`, and enqueues `PolicyUpkeep` expense.
- Initial daily-upkeep coefficients are fixed for v1 tuning and tested exactly:

| Policy | Base/day | Per hectare | Per eligible facility | Per activity unit |
|---|---:|---:|---:|---:|
| Focused Management | 250 | 0 | 0 | 0 |
| Enhanced Maintenance | 300 | 20 | 25 | 0 |
| Night Events | 400 | 15 | 20 | 0 |
| Quiet Operations | 200 | 10 | 10 | 0 |
| Landscape Protection | 250 | 30 | 0 | 0 |
| Premium Services | 350 | 0 | 20 | 0 |
| Major Event Attraction | 1000 | 30 | 50 | 0 |
| Enhanced Safety Audits | 400 | 0 | 20 | 0 |
| Environmental Regulation | 500 | 0 | 30 | 0 |
| Production Priority | 600 | 0 | 30 | 0 |
| Logistics Priority | 600 | 0 | 40 | 0 |
| 24-Hour Operations | 800 | 0 | 50 | 0 |
| Restricted Operating Hours | 250 | 0 | 10 | 0 |
| Advanced Automation | 1200 | 0 | 75 | 0 |

- Park `EligibleFacilityCount` uses the existing relevant managed-facility count; Industrial uses the Area's classified eligible facilities for that policy, not observed throughput or jobs. Geometry `m_SurfaceArea` is the hectare source. All `PerActivityUnit` coefficients are zero in the first release, so no occasionally unavailable observation metric is required for mandatory billing.
- Keep per-Area/per-policy fractional carry runtime-only and clear interpolation baseline on load; never back-charge unobserved time.

- [ ] **Step 1: Write failing XP/upkeep tests**

Cover +10%, Park 40/day cap, Industrial 240/day cap, zero award, long saturation, Suspended no upkeep, effect-unavailable no upkeep, conflict switch same boundary no double charge.

- [ ] **Step 2: Verify RED**

- [ ] **Step 3: Implement XP helper and progression integration**

Keep policy lookup in systems; keep pure progression models parameterized, not dependent on World/ECS.

- [ ] **Step 4: Implement policy upkeep settlement**

Register after policy runtime state is valid and before AreaFinanceSystem consumes the batch according to Task 4's verified order.

- [ ] **Step 5: Run focused + full tests**

- [ ] **Step 6: Commit**

`git commit -am "feat: add policy upkeep and focused management"`.

---

### Task 7: Park Policy Effects

**Files:**
- Create: `src/CS2AreaSystem/Modules/Park/ParkPolicyEffects.cs`
- Create: `src/CS2AreaSystem/Modules/Park/ParkPolicyEffectSystem.cs` only for effects requiring runtime observation/application.
- Modify: `src/CS2AreaSystem/Modules/Park/ParkFinanceModel.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkQualityModel.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkQualitySystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkPolicyCatalog.cs`
- Modify: `src/CS2AreaSystem/Modules/Park/ParkAreaModule.cs`
- Create: `docs/verification/park-policy-effect-contracts.md`
- Test: `tests/CS2AreaSystem.Tests/ParkPolicyEffectTests.cs`
- Modify: `tests/CS2AreaSystem.Tests/ParkFinanceModelTests.cs`
- Modify: `tests/CS2AreaSystem.Tests/ParkQualityModelTests.cs`

**Interfaces and required directions:**
- Enhanced Maintenance (2001): Area-internal and always available once unlocked. Increase effective management/maintenance input only by increasing real Park management spend/coverage; never post-add Quality points.
- Landscape Protection (2004): Area-internal. Modify landscape-oriented input and usable capacity through named constants in `ParkPolicyEffects`; no post-score Quality bonus.
- Premium Services (2005): Area-internal. Add `ParkFinanceModel.ComputeValueForMoney(int fee, float quality, double premiumServicesFactor)` or equivalent parameterized API; effect reduces high-fee value-for-money penalty, with `1.0` meaning vanilla Area behavior.
- Night Events (2002), Quiet Operations (2003), Major Event Attraction (2006): verify whether their visitor/attractiveness/noise/crowding directions can touch a safe 1.6.2f1 vanilla path. Each gets its own documented adapter contract if enabled.
- If a vanilla path is not proven, leave that policy definition visible but `EffectAvailable=false`; do not fake visitor/noise state in Park score inputs.
- Exact balancing values are named constants in `ParkPolicyEffects`, documented in policy descriptions, and covered by tests.

- [ ] **Step 1: Write Area-internal Park policy tests**

Assert underlying input changes and unchanged behavior at no-policy baseline.

- [ ] **Step 2: Inspect 1.6.2f1 candidate vanilla paths for Night/Quiet/Major Event**

Document exact consumer semantics before any writes.

- [ ] **Step 3: Implement verified Area-internal effects first**

- [ ] **Step 4: Implement only verified vanilla adapters**

For every unverified candidate, add an availability test proving it remains disabled instead of silently doing nothing.

- [ ] **Step 5: Run all Park tests + API-check build**

- [ ] **Step 6: Commit**

`git commit -am "feat: add park area policy effects"`.

---

### Task 8: Industrial Vanilla-Effect Policy Adapters

**Files:**
- Create: `src/CS2AreaSystem/Modules/Industrial/Policies/IndustrialPolicyAdapter.cs`
- Create one focused adapter file per verified effect family under `src/CS2AreaSystem/Modules/Industrial/Policies/` rather than one giant switch.
- Create: `src/CS2AreaSystem/Modules/Industrial/IndustrialPolicyEffectSystem.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialPolicyCatalog.cs`
- Modify: `src/CS2AreaSystem/Modules/Industrial/IndustrialAreaModule.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Create: `docs/verification/industrial-policy-effect-contracts.md`
- Test: `tests/CS2AreaSystem.Tests/IndustrialPolicyAdapterTests.cs`
- Modify: `tests/CS2AreaSystem.Tests/IndustrialApiContractTests.cs`
- Modify: `tests/CS2AreaSystem.Tests/IndustrialObservationTests.cs`
- Modify: `tests/CS2AreaSystem.Tests/IndustrialPerformanceModelTests.cs`

**Interfaces:**
- Base adapter contract:
  - `int PolicyId { get; }`
  - `bool IsSupported(Entity area, in AreaPolicyContext context, out string reasonKey)`
  - `void Apply(Entity area, in AreaPolicyContext context)`
  - `void Remove(Entity area, in AreaPolicyContext context)`
- The system applies/removes adapters only on effective-state transitions and on load/rebuild; it must be idempotent.
- Adapter failure disables only that policy effect for that Area/load generation and is logged once per `AreaStableId + PolicyId + failure class`.
- Policies and required directions:
  - 3001 Enhanced Safety Audits: stable/safe operation trade-off only if an exact vanilla control path exists.
  - 3002 Environmental Regulation: environmental burden down, modest production/efficiency trade-off.
  - 3003 Production Priority: production/efficiency up, environmental burden up.
  - 3004 Logistics Priority: real input/output flow improvement only through a proven logistics control path.
  - 3005 24-Hour Operations: operating activity up, noise/operating burden up.
  - 3006 Restricted Operating Hours: night/noise burden down, total productive activity down.
  - 3007 Advanced Automation: productivity/efficiency up, labor demand down.
- Never write Industrial Performance/Production/Logistics/Employment/Efficiency scores directly.
- After a verified adapter changes real state, existing `IndustrialObservationSystem` remains the only producer of observed metric changes.
- Unverified adapters remain unavailable and non-billable.

- [ ] **Step 1: Inspect Game.dll and document each candidate target/consumer/unit**

Do not group “member exists” with “semantics proven”. The document must say how vanilla consumes the field and how removal restores baseline.

- [ ] **Step 2: Write API/adapter failing tests**

Include idempotence, remove/restore, unavailable target, no direct score writes, and failure isolation.

- [ ] **Step 3: Implement adapter registry/effect system**

- [ ] **Step 4: Implement verified policies one at a time**

After each adapter, run its focused tests before moving to the next. Leave unverified policies disabled.

- [ ] **Step 5: Add observation regression tests**

Synthetic tests must show the policy layer does not bypass observation: only changed observation inputs lead to changed scores.

- [ ] **Step 6: Run Industrial tests + full API-check verification**

- [ ] **Step 7: Commit**

`git commit -am "feat: add verified industrial policy adapters"`.

---

### Task 9: Finance Detail Presentation, Localization, Offline Guards, and Runtime Verification

**Files:**
- Create: `src/CS2AreaSystem/Core/Finance/AreaBudgetBindingSystem.cs`
- Create: `src/CS2AreaSystem/Core/Finance/AreaBudgetViewModel.cs`
- Modify: `src/CS2AreaSystem/Mod.cs`
- Modify: `src/CS2AreaSystem/Core/UI/NativeAreaLocalization.cs`
- Create: `UI/src/AreaBudgetDetails.tsx`
- Create: `UI/src/AreaBudgetDetails.test.tsx`
- Modify: `UI/src/index.tsx`
- Modify: `UI/src/bindings.ts`
- Modify: `UI/src/localization.ts` only if required by the existing fallback-localization mechanism
- Modify: `scripts/test-offline-contracts.mjs`
- Modify: `scripts/runtime-smoke-checklist.md`
- Create: `docs/verification/area-budget-ui-contracts.md`
- Create: `docs/verification/area-policy-system-runtime.md`
- Modify fixtures/tests as needed:
  - `UI/src/__fixtures__/park-panel.json`
  - `UI/src/__fixtures__/industrial-panel.json`
  - `tests/CS2AreaSystem.Tests/ParkPanelFixtureTests.cs`
  - `tests/CS2AreaSystem.Tests/IndustrialPanelFixtureTests.cs`

**Interfaces / completion evidence:**
- `AreaBudgetViewModel` fields: `long AreaManagement`, `long AreaServiceFees`; both come directly from `AreaFinanceSystem` settled totals for the current budget reporting window.
- `AreaBudgetBindingSystem` publishes `cs2AreaSystem/areaBudget` and never moves money itself.
- `AreaBudgetDetails` is the only Area-specific budget-presentation component. It receives the authoritative values from the binding and extends the verified vanilla Budget UI presentation.
- Before implementing `AreaBudgetDetails`, inspect the supplied 1.6.2f1 UI bundle/runtime modules and document in `area-budget-ui-contracts.md` the exact Budget component/module extension point, the structure of the **Service Upkeep** expense group and **Service Fees** income group, and how their displayed subtotals/total income/total expense are computed.
- Target presentation is exactly:
  - Expenses → Service Upkeep → **Area Management**
  - Income → Service Fees → **Area Service Fees**
- Because `IncomeSource` is fixed and there is no generic ServiceFee source, `AreaBudgetDetails` must not inject Park admission as `FeeParking`, `FeePublicTransport`, or any other existing source. It appends the distinct Area row from `AreaServiceFees` and reconciles the displayed Service Fees subtotal/total income using that same already-settled real city transaction.
- For expenses, if Task 4's verified public service-upkeep route already makes Area expense part of the native Service Upkeep subtotal/total, the UI extension adds only the distinct **Area Management** detail row and must not add the amount a second time. If Task 4 had to use the version-pinned fallback accounting boundary, the UI extension reconciles the displayed Service Upkeep subtotal/total exactly once from `AreaManagement`.
- The UI component must fail closed: if the exact 1.6.2f1 Budget extension point cannot be verified, do not render an out-of-category substitute and record runtime failure instead.
- Localize all policy names/descriptions/lock reasons/effect-unavailable/suspended labels in the project's supported locale pattern.
- Budget detail labels are exactly “Area Management” and “Area Service Fees” in English, with project-consistent Japanese translations.
- Extend offline guards to catch:
  - reintroduction of direct Park management available-balance clamping;
  - Park admission being mapped to `FeeParking`, `FeePublicTransport`, or any unrelated `IncomeSource`/PlayerResource;
  - direct Industrial score mutation from policy files;
  - policy definitions without positive base upkeep;
  - Tourism policy registration;
  - missing policy/finance system registration or required order.
- Runtime document records actual observed results, game version/build, chosen finance adapter mode, and any policies intentionally left effect-unavailable.

- [ ] **Step 1: Write budget binding/UI/localization/offline-contract failing tests**

Assert the two authoritative values, exact labels, no unrelated fee-source mapping, exactly-once subtotal/total reconciliation, and no rendering outside the intended vanilla groups.

- [ ] **Step 2: Verify and document the 1.6.2f1 Budget UI contract**

Use the supplied UI reference/runtime registry. Record exact paths/exports/props or extension boundaries; do not infer them from decompiled C# type names.

- [ ] **Step 3: Implement binding and verified Budget UI extension**

The component reads settled values only. It never mutates city money and never invents a finance amount.

- [ ] **Step 4: Run full automated verification**

Run:
`powershell -ExecutionPolicy Bypass -File scripts/verify.ps1 -ApiCheck`

Expected: source guards PASS, offline contracts PASS, NUnit PASS, C# API-check build PASS, Vitest PASS, UI production build PASS.

- [ ] **Step 5: Build/deploy with the normal official toolchain**

Use the user's configured CS2 paths/toolchain. Do not call the API-check artifact playable.

- [ ] **Step 6: Execute the runtime checklist on 1.6.2f1**

Record at minimum:
1. policy UI interaction/visual parity with District policies;
2. per-Area independent policy state;
3. atomic conflicts;
4. locked Level behavior;
5. Suspended → Active preservation/resume;
6. Service Upkeep contains real Area Management expense exactly once;
7. Service Fees contains Area Service Fees backed by the real admission city credit and not an unrelated fixed fee source;
8. top-level displayed income/expense totals reconcile with the city-money changes and Area rows;
9. expense continues while city balance is negative;
10. no Park building upkeep double-charge;
11. Park admission household debit equals city credit equals Area Service Fees amount;
12. each enabled vanilla-effect policy changes its documented real target;
13. Industrial scores move only through observation;
14. save/load restores desired/effective state and finance without back-charge;
15. unverified adapters remain visibly unavailable and non-billable.

- [ ] **Step 7: Update runtime evidence document with PASS/FAIL per item**

Do not mark a policy or finance presentation complete without a real runtime observation.

- [ ] **Step 8: Run final automated verification again after runtime-driven fixes**

Expected: full verification PASS.

- [ ] **Step 9: Commit**

`git commit -am "test: verify area policy system integration"`.

---

## Final Acceptance Checklist

Before opening/merging a PR or treating the work as implemented:

- [ ] All Task 1–9 commits exist and are reviewable independently.
- [ ] `scripts/verify.ps1 -ApiCheck` passes from a clean checkout with the supplied 1.6.2f1 Managed path.
- [ ] Normal official-toolchain build succeeds and deploys current UI.
- [ ] Runtime evidence identifies which vanilla-effect policies are enabled and which remain unavailable.
- [ ] No unverified policy is presented as working.
- [ ] Park management expense no longer uses the old available-balance clamp.
- [ ] Real Area expenses change city money and are reconciled into Service Upkeep; real Area income changes city money and is reconciled into the Service Fees presentation.
- [ ] Park admission is not mislabeled as any unrelated fixed `IncomeSource` or `PlayerResource`.
- [ ] “Area Management” / “Area Service Fees” detail presentation is backed by the exact same settled real transactions and is counted exactly once.
- [ ] Negative city balance does not suppress Area expense.
- [ ] Unknown policy IDs survive save/load inertly.
- [ ] Conflict swaps are atomic.
- [ ] Per-Area configuration is independent.
- [ ] Tourism remains policy-free.
- [ ] Existing unresolved runtime issues (#30, #38, #43, #44) are not silently declared fixed unless this work actually reproduces and verifies them.
