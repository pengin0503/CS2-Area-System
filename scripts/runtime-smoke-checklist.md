# Area Core — Cities: Skylines II 1.6.2f1 runtime verification

**Current activation rules:** Phase 3 C2 supersedes the Foundation + Phase 1 anchor rules and every earlier Park Entrance / Industrial HQ instruction below: a Park needs an `AS_ParkHeadquarters` Headquarters **and** an `AS_ParkGate` Entrance; an Industrial Area needs an `AS_IndustrialHeadquarters` Headquarters. Park Maintenance, office buildings and dedicated Path crossings no longer activate anything. Earlier dated scenarios (including Foundation F01–F07 and P07's Entrance work bound) remain regression references with these prerequisites substituted; the path-crossing cases are retired. C1 also supersedes the old unsaved-trend assumption.

Status: **PASS — user-confirmed 2026-10-04.** The user reports every in-game item in this section was completed on Cities: Skylines II 1.6.2f1 and behaved normally. Exact local save/build metadata was not supplied in this chat; automated/API evidence remains separate.

1. Areas menu appears in the bottom construction UI without editing shipped UI files.
2. Park, Industrial, Tourism selections activate native AreaToolSystem.
3. Create, vertex-edit, complete, re-edit, and delete each managed area type. Stable ID/type survive edits; boundary changes do not clear vanilla buildings/props. Park without both Park Maintenance Headquarters and Entrance evidence => Suspended; provide both => Active; Tourism completes => Active; its asset tooltip and selected-panel summary explicitly say boundary only and that tourism gameplay is not implemented.
   - Issue #1: completed Space areas are not raycast by the vanilla default tool. Open **Managed areas** after leaving the draw tool and after a load, pick each area and verify that the vanilla selected-info panel opens with the area section (type, status, entrance/headquarters, progress, Edit/Delete) below the vanilla header, the camera moves to it, renaming via the vanilla header and locate via the vanilla actions work, and the section refreshes immediately after assignment, cancel and lifecycle changes. Check the list/hint placement does not overlap vanilla UI at common resolutions, and that the vanilla panel still works if the area section fails.
   - Issue #13: Edit from the area section must use normal vertex manipulation, not start a polygon redraw seeded with two original vertices. Cancel must preserve the original geometry, stable ID and anchor/progress; it must not switch to an unrelated Object Tool placement mode. For nearby/overlapping areas, verify the native pointer targets the intended boundary.
4. Build a Park inside a Tourism Area and verify both memberships exist. Confirm native overlap notifications are warnings and completion succeeds. Check warning/icon/ECB ordering using keyboard/mouse and gamepad controls.
5. Create overlapping Park Areas and verify exclusive membership primary owner is the smallest persisted stable ID, including after load. Alongside permitted overlap, try an invalid/self-intersecting boundary and an unpurchased city tile: their native errors must still prevent completion. Check that vanilla Space/other Area conflicts and vanilla asset tools retain their original behavior.
6. Create Industrial Area: no HQ => Suspended; assign vanilla building inside it => Active; delete HQ => only its Industrial Area Suspended (progress unchanged); reassign => Active. (Foundation requires an office building; former industrial-facility HQ links are invalid. See the Foundation section.)
7. Save and reload; stable IDs/type/lifecycle/settings/progress/anchor remapping remain and membership rebuild count increments exactly once for load. Include a schema-v1 TypeId=9999 fixture: identity preserved and ModuleUnavailable.
8. Create/move/delete one candidate building and verify incremental counters change without FullRebuildCount increasing. Resize/shrink an Area and verify old memberships disappear and new candidates are found.
9. Temporarily break/disable the UI bundle and verify the save still loads and area simulation/state remains intact.
10. Confirm normal roads, paths, vegetation, and vanilla assets remain in their original menus rather than being duplicated.
11. Start the game with the game language set to ja-JP and then en-US, open the Areas menu and a managed area in the selected-info panel, then exit the game. Menu, asset and panel text must be localized (no raw `Assets.NAME[...]`/`CS2AS.*` keys), the Areas menu/Area Types/three asset icons must render, and `Logs/CS2AreaSystem.log`/`Player.log` must contain no `Area menu localization unavailable`, `Error disposing mod CS2AreaSystem` or `ResourceHandler: Invalid host locations map` errors (issues #15, #16). On 1.6.2f1 no `Area icon ... unavailable` or `Game UI resources could not be verified` warning may appear; if one does, the runtime icon lookup (#8) must be re-checked.
12. Open the Areas toolbar menu in a fresh session and after loading a save: the **Area Types** tab (with icon) and the Park/Industrial/Tourism assets must be listed. Hovering the menu button, the tab and each asset must show a localized tooltip. Then open **Options → CS2 Area System**: every option has a localized label/description; toggling each option (initial list expansion, camera focus, panel side, delete confirmation, managed overlap) takes effect without restarting and persists after restarting the game (`Settings/ModsSettings/CS2AreaSystem/CS2AreaSystem.coc`). With initial list expansion off, the **Managed areas** button stays visible: open it, select an existing area, assign its anchor and edit/delete it; the next UI update must not collapse a manually opened list. With the option on, manual collapse must also survive subsequent updates. Loading a save or starting a new session reapplies the saved expansion preference.

Diagnostic access: ECS systems expose `AreaMembershipSystem.FullRebuildCount`, `IncrementalMemberUpdateCount`, `IncrementalAreaUpdateCount`, `Index.GetAreas(member)`, `Index.GetMembers(stableId)` and `AreaLoadCoordinatorSystem.RebuildPending`. Use the mod/debug tooling; there is no diagnostics UI in v1.

# Park Area v1 — Cities: Skylines II 1.6.2f1 runtime verification

Status: **PASS — user-confirmed 2026-10-04.** The user reports every in-game item in this section was completed on Cities: Skylines II 1.6.2f1 and behaved normally. Exact local save/build metadata was not supplied in this chat; automated/API evidence remains separate.

Diagnostic access: `ParkRuntimeSystem.Store` (per-Park landscape/facility/quality/visitor snapshots), `ParkVisitSystem.Tracker` (`SessionCount`, `ActiveCount(id)`), `ParkVisitSystem.ObservedLastUpdate`, `ParkLandscapeSystem.ScanCount`, plus the Park tabs in the selected-info panel.

1. Draw a Park Area with no Entrance → **Suspended**; Overview shows the NoEntrance warning; Level/XP rows show Level 1 / 0.
2. Assign an eligible Entrance (a park, leisure or attraction building inside the area) → **Active**. Try an unrelated building (house, office, factory) and a building outside the area: both must be rejected and the assignment stays pending.
3. Add a second valid Entrance (Add entrance) → remains Active, the entrance list shows both, Accessibility rises (more for entrances far apart than for two adjacent ones).
4. Remove the first Entrance with its Remove button (or demolish it) → remains Active while the second survives.
5. Remove/demolish all Entrances → **Suspended**; Level, XP, lifetime visitors, admission settings, budget and statistics are unchanged; no management cost is charged and no XP is earned while suspended.
6. Add real park/leisure/attraction buildings inside → Facility categories, Attractiveness and Variety change within a few in-game minutes. Unrelated buildings inside must not change them.
7. Place trees, benches/tables, statues/fountains, lamps and footpaths → Landscape tab categories change after the next scan (props from the vanilla Park and Stone prop menus count as Decoration; confirm those menu categories resolve as `PropsPark`/`PropsStone` in 1.6.2f1, and that industrial/commercial props add no Decoration); `AreaMembershipSystem.Index.GetMembers(id)` still contains buildings only (no trees/props).
8. Spam one tree prefab (hundreds) → Vegetation saturates rather than growing linearly; extreme density shows the clutter warning with a bounded penalty.
9. Watch real citizens doing leisure at member park/leisure/attraction buildings → Visitors now / Visits (last day) / Total visits increase; satisfaction starts near 50 and moves as visits complete.
10. Workers at unrelated member buildings and residents living inside the boundary, and people only walking through, must not produce visits.
11. Tourists (households with `TouristHousehold`) are counted under Tourist visits, residents under Resident visits.
12. Create two overlapping Park Areas sharing a facility → the visit is counted, charged and awarded XP only in the Park with the smaller stable ID; the other Park shows the "overlaps another Park" warning.
13. Put the Park inside a Tourism Area → Park counts are unchanged; the Park's Tourism contribution value is shown and vanilla tourism/attractiveness is not modified.
14. Reach Level 2 (Neighborhood Park), set an admission fee → for a new visit the visitor household's money decreases by exactly the collected amount and city money increases by the same amount, once per visit (verify with a small city and the household/city money values). A household without enough money pays at most what it has and never goes below zero.
15. Free Park (fee 0, or any fee while Level 1) charges nothing; the fee slider is disabled at Level 1.
16. Raise the fee on a mediocre Park → Value-for-money (and thus satisfaction of new visits) falls, while where citizens choose to go is not intentionally changed (no destination filtering).
17. Change the Park budget 50% ↔ 150% → Area management cost per day and money paid change proportionally; Maintenance falls below 100% and rises with diminishing returns above; no other Quality metric changes directly. Vanilla upkeep of the member buildings is not charged again (compare the vanilla budget panel).
18. Save and reload → admission fee, lifetime totals, satisfaction, Level/XP, budget, entrances and promotion hold are preserved; landscape/facility/quality values rebuild within a few in-game minutes (Calculating/neutral in between); recent statistics restart.
19. Save while a paid visit is in progress and reload → that visitor is not charged again until a genuinely new visit.
20. Large city profiling (100k+ population, several Parks): no noticeable frame-time increase; `ParkVisitSystem` processes one of 16 citizen groups every 64 simulation frames and never scans all citizens × all Parks; `ParkLandscapeSystem` scans one Park at a time, classifies at most 4096 candidate objects per frame and starts a new scan at most every 8 frames. Also check a very large Park (tens of thousands of trees) causes no visible hitch.

21. Load a save made before Park v1 whose Park entrance is an arbitrary building (house/office): the Park is Suspended, the summary says no assigned entrance qualifies, the old entrance is listed as "Not eligible" with Remove, and removing it then assigning a park/leisure/attraction building reactivates the Park. Demolish an entrance building: it is listed as "Building removed" and can be removed.

Additional UI checks: all five Park tabs render in ja-JP and en-US without raw `CS2AS.*` keys; the vanilla slider (or the +/− fallback) sends budget/fee changes only for the selected Park; a failing Park tab hides only that tab content while Type/Status/Edit/Delete keep working. Park Events/Festivals are intentionally absent. Park policies are covered by the Area Policy System section below.

# Industrial Area v1 — Cities: Skylines II 1.6.2f1 runtime verification

Status: **PASS — user-confirmed 2026-10-04.** The user reports every in-game item in this section was completed on Cities: Skylines II 1.6.2f1 and behaved normally. Exact local save/build metadata was not supplied in this chat; automated/API evidence remains separate.

Diagnostic access: `IndustrialRuntimeSystem.Store` (per-Area published `IndustrialObservationResult`, flow windows, `DelayedCycles`), `IndustrialRuntimeSystem.Structure`, `IndustrialObservationSystem.Scheduler` (`ActiveCycles`, `RestartCount`, `Cycle(id)`), `AreaMembershipSystem.Index.GetAreaRevision(id)`, plus the Industrial tabs.

- [ ] 1. Draw an Industrial Area.
- [ ] 2. It remains **Suspended** without an HQ (Overview warning, summary asks for an extractor/factory/warehouse/cargo building).
- [ ] 3. Assigning an eligible HQ (extractor, factory, warehouse or cargo building inside the area) activates it.
- [ ] 4. An ineligible HQ assignment (office, house, park, civic building, building outside the area) is rejected and the assignment stays pending; an accepted HQ remains valid through tenant vacancy, bankruptcy and poor operating health (the Area stays Active).
- [ ] 5. HQ deletion suspends the Area (Level/XP/history unchanged, Overview shows paused values); reassignment restores Active without duplicating XP.
- [ ] 6. Extractor classification (specialized industry extractor building with its company; extractor upgrades) — Production tab Extractors count.
- [ ] 7. Processor/manufacturer classification (zoned industrial factories) — Processors count; office buildings are not counted.
- [ ] 8. Storage classification (zoned warehouses) — Storage facilities count and stored/capacity plausible versus the vanilla storage section.
- [ ] 9. Cargo facility classification (cargo train station/harbor/airport inside the area, operating and disabled) — Cargo facilities operating/all.
- [ ] 10. Real jobs/employees match visible game behavior (compare the vanilla Employees section of the member buildings).
- [ ] 11. Efficiency/profitability move plausibly with vanilla problems (cut electricity/water, raise industrial taxes) and match the vanilla Efficiency/Profitability sections' direction.
- [ ] 12. Production/throughput responds to actual operation rather than placement: an unstaffed or input-starved factory lowers Productive activity; per-resource rates are comparable with the vanilla company production values.
- [ ] 13. Input shortage (remove suppliers/cut freight access) produces the Input starvation diagnostic after enough observed hours.
- [ ] 14. Storage/output pressure (full extractor/factory storage) produces the Output/storage saturation diagnostic.
- [ ] 15. Same-kind overlap does not double-count: two overlapping Industrial Areas share a factory → jobs/production/XP only in the smaller-ID Area; the other shows overlapping buildings.
- [ ] 16. Save/load preserves Level/XP/milestones, the day's XP count, last settled hour and streak.
- [ ] 17. Derived caches rebuild after load (Overview waits for the first observation; no pre-load values reappear as current).
- [ ] 18. UI tabs update without stale entity references (select different Areas, delete members, reload).
- [ ] 19. A large Industrial Area (hundreds of buildings, 100k+ population city) does not cause visible recurring stalls; record observation cycle durations and any Delayed cycles.
- [ ] 20. Park and Tourism/Core remain operational if Industrial observation is unavailable (force an exception or remove the Industrial systems).
- [ ] 21. A healthy industry using external cargo and no dedicated warehouse receives no infrastructure-presence penalty (Logistics 100 when flows are healthy).
- [ ] 22. Ordinary office-only employment inside the boundary does not inflate Industrial job/employee totals.
- [ ] 23. One missing XP input permits reweighted XP (e.g. Production unavailable, others present); two missing inputs show the zero-XP reason.
- [ ] 24. Daily cap 240 is reached and holds until the next game day; same-hour save/reload does not duplicate XP.
- [ ] 25. Fewer than 18 observed hours and a brief daily spike do not satisfy the seven-day streak (Progress tab shows the reason).
- [ ] 26. Other-Area and Park edits do not restart this Area's cycle, while a primary-ownership transfer invalidates both affected Industrial Areas (`Scheduler.RestartCount`, `Cycle(id).CycleId`).
- [ ] 27. Synthetic calculation examples match the models (automated); actual company/flow field semantics and coverage are validated separately here — record whether `CompanyStatisticData.m_LastUpdateProduce`, `GetCompanyProductionPerDay`, the LackResources/storage-limit symptoms and `RentersUpdated` behave as documented.

Additional UI checks: all five Industrial tabs render in ja-JP and en-US without raw `CS2AS.*` keys (resource names come from the game's `Resources.TITLE[...]`); a failing Industrial tab hides only that tab's content while Type/Status/HQ/Edit/Delete keep working. Industrial budgets, subsidies and direct buffs are intentionally absent. Industrial policies are listed but unavailable (Area Policy System section below).

# Area Policy System — Cities: Skylines II 1.6.2f1 runtime verification

Status: **PASS — user-confirmed 2026-10-04.** The user reports every in-game item in this section was completed on Cities: Skylines II 1.6.2f1 and behaved normally. Exact local save/build metadata was not supplied in this chat; automated/API evidence remains separate.

1. The policy section looks and behaves like the vanilla District policies section. It is not a floating window.
2. Two Areas of the same type keep independent policy states.
3. Conflict swaps are atomic: never both on, never both billed.
4. A policy above the Area's Level is locked. After a level-up it is available and OFF.
5. A Suspended Area keeps its desired states, while effects and upkeep stop. They resume on re-activation.
6. Budget page: Expenses → Service Upkeep includes **Area Management** once, with its own detail line.
7. Budget page: Income → Service Fees includes **Area Service Fees** once, backed by real admission, with its own detail line. The fixed fee lines are unchanged.
8. The displayed totals and balance reconcile with the city-money change. The budget chart excludes Area amounts (known limitation).
9. Area expense is charged in full while the city balance is negative.
10. No Park member building upkeep is charged twice.
11. Park admission: household debit = city credit = the Area Service Fees amount.
12. 1001/2001/2004/2005 change their documented targets.
13. Industrial scores move only through observation.
14. Save/load restores policy state and finance without a back-charge.
15. Policies 2002, 2003, 2006 and 3001–3007 are visibly unavailable and non-billable.

If a Budget Area row is missing, check `Player.log`/the UI console for "Area budget". The vanilla budget item ids are asset data; see `docs/verification/area-budget-ui-contracts.md`.

# Pre-Tourism Wave A1–A5 — Cities: Skylines II 1.6.2f1 runtime verification

Status: **PASS — user-confirmed 2026-10-04.** The user reports every in-game item in this section was completed on Cities: Skylines II 1.6.2f1 and behaved normally. Exact local save/build metadata was not supplied in this chat; automated/API evidence remains separate.

1. Trends (A1): after two in-game hours a Park shows "vs previous" rows under Quality, recent visits, satisfaction, occupancy, admission income and management expense; an Industrial Area shows them under Performance, the four components and active facilities. The first sample reads "first sample", and rising/falling/unchanged follows the visible values.
2. Trends (superseded by Foundation C1): after save → load up to the latest 24 completed hourly samples seed the trend rows from the retained 168-bucket history. A partially observed admission/expense hour must not create a false falling trend. See the Foundation history case below.
3. Industrial bottleneck focus (A3): "Show facility" under a suggestion moves the camera to a facility that matches that bottleneck (e.g. the understaffed or input-starved company), and changes nothing in the city (money, resources, policies, selection state otherwise unchanged).
4. The focus button rejects a facility that was demolished, moved out of the Area, or became primary-owned by another Industrial Area after the panel was built (no camera move, no error), and works again once the next observation cycle names a current facility.
5. Industrial processes (A4): Production tab recipe rows (inputs → output) match the vanilla company info section of a representative company for each listed process; a recipe with no measured rate this cycle shows "Unavailable", never 0.
6. Park landscape diagnostics (A5): removing most trees/benches/lights from a Park makes the matching shortage warning appear after the next landscape scan; adding them back clears it. Packing props tightly shows the existing clutter warning with "Show location" pointing into the dense spot.
7. Sparse amenity/lighting warnings appear only for a Park whose benches/lights sit in one corner of a well-landscaped Park, and "Show location" points to a landscaped spot without them.
8. Park "Show location" rejects a prop that was deleted or moved outside the Park after the scan; with Landscape Protection active, a vegetation/decoration shortage the policy offsets is not shown.
9. Performance: in a dense Park (thousands of props) and a large Industrial Area, frame time during the landscape scan and observation cycle is no worse than before Wave A (`ParkLandscapeSystem.MaxScanFrameWork` stays within the unchanged 4096-object budget plus one tile); no new frame spike while the panel is open.
10. ja-JP and en-US show no raw `CS2AS.*` keys for the new trend, process, diagnostic and focus rows.

# Foundation + Phase 1 — Cities: Skylines II 1.6.2f1 runtime verification

Status: **PASS — user-confirmed 2026-10-04.** The user reports every in-game item in this section was completed on Cities: Skylines II 1.6.2f1 and behaved normally. Exact local save/build metadata was not supplied in this chat; automated/API evidence remains separate.

| Case | Procedure | Expected result | Result |
| --- | --- | --- | --- |
| F01 Industrial Headquarters | Draw an Industrial Area containing an office and productive facilities. Assign the office; try a factory, extractor, warehouse and cargo building. Vacate/replace the office tenant, then remove the HQ. | Office accepted, non-office industrial candidates rejected. Vacancy does not suspend; removal does. Production/Logistics never count the office. Level/XP retained. | PASS (user-confirmed 2026-10-04) |
| F02 Legacy links | Load an older save with an Industrial factory HQ and Park leisure-building Entrance. Inspect/remove links and assign the new required roles. | Old links are visible as invalid/removable; no automatic conversion. StableId, Level/XP, finance, policy desires and saved history survive. | PASS (user-confirmed 2026-10-04) |
| F03 Park activation matrix | Test no HQ/no entrance; Maintenance HQ only; dedicated crossing Path only; both. Delete the HQ while retaining the Path. | Only both activate. Summary identifies the missing HQ/entrance or pending scan. HQ deletion suspends. | PASS (user-confirmed 2026-10-04) |
| F04 Path qualification | Test a dedicated Path crossing the boundary, inside-only and outside-only Paths, and an ordinary road whose sidewalk crosses the same boundary. | Only the dedicated crossing supplies automatic Entrance evidence. Road sidewalks remain usable by A6 diagnostics but cannot activate a Park. | PASS (user-confirmed 2026-10-04) |
| F05 Duplicates and geometry | Join dedicated Path segments at the same crossing, then edit Park vertices during a sliced scan (including an edit that leaves the AABB unchanged). | Nearby duplicate points collapse. Evidence from an incompatible boundary is discarded; incomplete scans never fabricate an entrance. | PASS (user-confirmed 2026-10-04) |
| F06 Network edits | Delete the sole crossing Path; reconnect it. Repeat during a scan and after load; wait for bounded completion/periodic reconciliation. | Park suspends when completed evidence has no entrance and resumes when restored. Last compatible evidence may remain during net reconciliation; no old Net entity is authoritative after load. | PASS (user-confirmed 2026-10-04) |
| F07 Semantic providers | With a small v1 registry provider, allow a custom Maintenance HQ/Entrance and Park leisure role. Test conflicting priorities, provider removal and a provider failure while a Path entrance also exists. | Highest priority, then ordinal provider ID wins; removal invalidates caches/anchors and falls back to vanilla typed facts. Other providers continue. Registered and Path entrances coexist; removing a provider does not invalidate a qualifying Path. | PASS (user-confirmed 2026-10-04) |
| P01 Connectivity states | Add a reachable leisure facility and a disconnected facility. Observe a sliced scan; also test an unattached facility, missing net data and a scan exceeding the traversal cap. | Complete may list proven disconnected facilities. Partial/Unknown does not infer disconnection; unattached/unknown targets are identified. No citizen destination/pathfinding change. | PASS (user-confirmed 2026-10-04) |
| P02 Stale focus | Build the diagnostic panel, then delete/move the named facility, change primary ownership or edit boundary/entrance before clicking Show facility. | Stale focus is rejected without camera movement or entity mutation; a new completed result permits focus again. | PASS (user-confirmed 2026-10-04) |
| P03 What-if isolation | Record money, budget and fee. Move candidate budget/fee sliders; switch Areas and reload. Explicitly apply each candidate separately. Repeat at Level 1 and with Suspended/unavailable samples. | Preview changes no authoritative settings/money and is labeled estimated. Explicit apply uses current selection/stable-ID guards. Level/availability constraints hold; transient candidates do not become save state. | PASS (user-confirmed 2026-10-04) |
| P04 What-if estimates | Compare candidate cost, coverage, Maintenance, value-for-money and net with current values and policy multipliers. | Estimates hold current visitor volume/Quality constant; missing observations are unavailable rather than zero. Demand/household solvency is not predicted. | PASS (user-confirmed 2026-10-04) |
| P05 Persistent history | Advance over seven game days, save/load, then advance another hour. Inspect both module buffers and trend rows; repeat save in the same hour and test an unsupported history schema in a disposable fixture. | At most 168 unique hourly buckets per Area; latest 24 seed trends after load, repeated hour updates without duplication. Unsupported data is isolated and not overwritten. No saved runtime Net identities. | PASS (user-confirmed 2026-10-04) |
| P06 Industrial comparison | Open an Area containing peers with current, suspended, delayed and older-than-two-hour data; compare every metric with each peer's own panel. Delete a peer or use a Tourism boundary. | Managed Industrial Areas only; stale/unavailable distinct from current zero. No simulation, selection or finance mutation. | PASS (user-confirmed 2026-10-04) |
| P07 Bounded performance | Profile a dense Park and many Areas through scan, network edits, selected-panel updates and save flush. Inspect Entrance LastWork/MaxWork and Connectivity LastWork. | No whole-city per-frame net scan. Entrance work <=2048 and connectivity work <=4096 per update; cap exhaustion remains partial/unknown. History <=168 per Area; ordinary updates persist <=4 Areas. | PASS (user-confirmed 2026-10-04) |
| P08 Regression and UI | Re-run A1–A5, overlap/primary ownership, policies, finance under negative city balance, unsupported-save isolation and native selection. Test ja-JP/en-US, separate HQ/Entrance groups and generated panel features in the deployed MOD. | Existing behavior preserved with revised activation/history prerequisites. No raw localization keys or incorrect zero/stale labels. Official postprocessing/deployment succeeds. | PASS (user-confirmed 2026-10-04) |

Do not change these Results to PASS without direct game evidence. C4/C5/C6, Phase 3 and Tourism gameplay are outside this checklist.


# Phase 2 B2 — Manual Industrial Company Assignment

Status: **PASS — user-confirmed 2026-10-04.** The user reports every in-game item in this section was completed on Cities: Skylines II 1.6.2f1 and behaved normally. Exact local save/build metadata was not supplied in this chat; automated/API evidence remains separate.

| Case | Procedure and expected result | Result |
| --- | --- | --- |
| B201 Company overlap | Put two eligible companies in one overlapping Building. Assign only one to the non-primary Area. Company metrics follow their separate owners; Core membership/primary, other company and static Building/cargo/efficiency ownership stay geometric; non-primary Company metrics do not inherit Building-wide efficiency or LackResources evidence. | PASS (user-confirmed 2026-10-04) |
| B202 Suspended primary | Suspend the geometric primary Area while the overlap Area is active. Companies candidates appear through bounded reads; assigning one starts observations for it. | PASS (user-confirmed 2026-10-04) |
| B203 Duplicate claims | Load duplicate valid claims with different StableIds. Lowest StableId wins once; removing a losing claim leaves winner unchanged. | PASS (user-confirmed 2026-10-04) |
| B204 Move/eligibility | Move an assigned company within/outside target, delete/reuse it, or change it to office/commercial. Claim becomes invalid, metrics fall back if eligible, invalid row can be removed; stale actions cannot affect a replacement. | PASS (user-confirmed 2026-10-04) |
| B205 Save/load/migration | Load an older save without assignment buffers; assign/save/load with Entity remapping. Empty buffers initialize, valid claims survive, runtime index rebuilds before consumption; Foundation/Phase 1 Level/XP/history/finance persist. | PASS (user-confirmed 2026-10-04) |
| B206 Revision/stale UI | Assign/remove mid-cycle, then click an old key or switch Area. Old cycle/coverage cannot earn XP, cycle restarts with current revision and no old time interval bridges; stale key/StableId is rejected. Unrelated Areas retain in-flight cycles, flow coverage and progression endpoints; removing a losing duplicate causes no restart. | PASS (user-confirmed 2026-10-04) |
| B207 Removal/cap/focus | Fill 64 candidate rows, include invalid/duplicate saved claims and refresh observations. Saved claim removal stays accessible; manual owner's current bottleneck focus works, deleted/moved/stale target does not. | PASS (user-confirmed 2026-10-04) |
| B208 Performance/regression | Profile many overlapping Areas and compare Foundation/Phase 1 behavior/UI ja-JP/en-US. Shared read work <=128/update, no second whole-world scan; record delayed cycles/frame timings. Native company assignment never changes economy/tax/pathfinding. | PASS (user-confirmed 2026-10-04) |
| B209 Missing saved references (#84) | Load a claim whose Company reference resolves to Null, with present and missing rented Building references. Its invalid row allows removal only in the selected Area, never assignment; other Company/Area claims survive. | PASS (user-confirmed 2026-10-04) |
| B210 Delayed ownership candidates (#85) | Start an observation at hour 5 and consume its pending read at hour 6. Companies rows remain visible with current-hour keys; hour-5 keys are rejected. Observation aggregation retains its original cycle/hour identity. | PASS (user-confirmed 2026-10-04) |

# Phase 2 C4 — Industrial Support Payments

Status: **PASS — user-confirmed 2026-10-04.** The user reports every in-game item in this section was completed on Cities: Skylines II 1.6.2f1 and behaved normally. Exact local save/build metadata was not supplied in this chat; automated/API evidence remains separate.

| Case | Procedure and expected result | Result |
| --- | --- | --- |
| C401 Setting and UI | Open the Support tab of an active Industrial Area; use the slider (or -/+ fallback) across 0–5,000 in 250 steps; switch Areas mid-change; check ja-JP/en-US. Only `DailySupportPerCompany` changes; 0 shows Off; stale selection and quarantined Areas reject the action; a failed module registration shows a warning and no control; no raw keys. | PASS (user-confirmed 2026-10-04) |
| C402 Daily request | Set 1,000 with N resolved companies and wait for the next in-game day. Exactly one Area Management expense of N × 1,000 per Area/day appears; LastScheduledDay advances only after enqueue; no request while the roster is calculating, partial, stale or the Area is inactive; skipped days are not back-paid. | PASS (user-confirmed 2026-10-04) |
| C403 Full settlement and credit | With a negative city balance away from the native lower bound, confirm the full request is debited once and each still-owned receiver's Money rises by the per-company amount; lifetime Settled = Credited + Unallocated. | PASS (user-confirmed 2026-10-04) |
| C404 Partial settlement | Bring city money near `-PlayerMoney.kMaxMoney` so half the request settles. Credits total exactly the settled half (equal split, earlier Entity Index/Version get remainders, each capped at the setting). | PASS (user-confirmed 2026-10-04) |
| C405 Receiver changes | Delete one receiver, move another out of the Area, and reassign a third with B2 between request and credit. Their shares are unallocated, never given to a newcomer; the next day follows the new resolved owners only; no exception or replay. | PASS (user-confirmed 2026-10-04) |
| C406 Save boundary and load | Save immediately after the daily debit (before the credit job completes), then load. The credit and lifetime counters are recorded exactly once before serialization; loading on the same day schedules nothing again; nothing pending is replayed. Loading a save without support state initializes default 0. | PASS (user-confirmed 2026-10-04) |
| C407 Regression and performance | Re-run B2 assignment, Industrial observations/XP, Park finance and Area budget rows with support active. Shared read budget and observation cadence unchanged; no second whole-world scan; no tax, demand, production or profitability writes. | PASS (user-confirmed 2026-10-04) |

# Phase 2 C5 — Park Festival (internal Park event)

Status: **PASS — user-confirmed 2026-10-04.** The user reports every in-game item in this section was completed on Cities: Skylines II 1.6.2f1 and behaved normally. Exact local save/build metadata was not supplied in this chat; automated/API evidence remains separate.

| Case | Procedure and expected result | Result |
| --- | --- | --- |
| C501 Start and cost | Wait for the first management settlement after load (cost shows Calculating before it). Start the festival; click again while pending. Exactly one Area Management expense of max(1, round-to-even(50% of daily management)) appears once; the second click is refused; status Pending → Running only after payment. | PASS (user-confirmed 2026-10-04) |
| C502 Zero settlement | Put city money at the native lower bound (`-PlayerMoney.kMaxMoney`) and start. Nothing is charged, no festival starts, no history line; Start is available again (no automatic retry). With a negative balance away from the bound, the festival is paid in full. | PASS (user-confirmed 2026-10-04) |
| C503 Effects | During the festival compare with just before: comfortable capacity ×0.85 (with Landscape Protection ×0.85×0.85), crowding/Comfort follows; completed-visit satisfaction samples +5 (max 100); visit XP ×1.25 with Focused Management composition; passive XP and its daily cap unchanged; Attractiveness, vanilla attraction and citizen destinations unchanged. | PASS (user-confirmed 2026-10-04) |
| C504 Duration and suspension | Start late in the evening and confirm the festival lasts a full in-game day, not until midnight. Suspend the Park mid-festival (remove HQ), then restore it: effects stop immediately while time continues; it resumes only inside the original interval and ends at the saved end frame; Quality returns to neutral on expiry. | PASS (user-confirmed 2026-10-04) |
| C505 Save/load | Save while pending (then load): the city charge and start are final before the save or the request is cleared with no charge after load. Save while running and load: end frame, paid cost and history survive; no second charge; Lifetime totals unchanged. Load an older save without event data: neutral, Start available after the cost estimate. | PASS (user-confirmed 2026-10-04) |
| C506 Eligibility and deletion | Suspend the Park after starting but before payment settles: the paid amount is accounted (Total event cost) without effects or history. Delete a Park with a running festival: no errors; its state/history disappear with the Area. | PASS (user-confirmed 2026-10-04) |
| C507 Regression and UI | Re-run Park finance/admission, policies, Quality/Progression and the Industrial C4 support with festivals; en-US/ja-JP Events tab has no raw keys; a failed event-module registration shows the unavailable warning while base Park gameplay continues. | PASS (user-confirmed 2026-10-04) |

## Phase 2 C6 Park coverage operating modifier (release gate)

Status: **PASS — user-confirmed 2026-10-04.** All C601–C606 release-gate cases were reported normal on Cities: Skylines II 1.6.2f1. The release gate is now open and the modifier has a live Options off switch. Automated/API evidence remains separate from this in-game result.

| Case | Procedure and expected result | Result |
| --- | --- | --- |
| C601 Options off | Turn **Enable Park coverage modifier** off in Options. The Finances tab reports the modifier disabled; park-building coverage returns to vanilla values and no modifier write remains after the next valid writer pass. | PASS (user-confirmed 2026-10-04) |
| C602 Enable and budget | Turn **Enable Park coverage modifier** on. At 100% budget nothing changes; at 150% and 50% coverage strength/capacity change by ×1.10/×0.90 after the next native Park pass (≤1024 frames), coverage range unchanged; repeated passes over an in-game day do not drift (no compounding); Enhanced Maintenance at 150% stays at ×1.10. | PASS (user-confirmed 2026-10-04) |
| C603 Disable and failure | Turn the Options switch off mid-game: tracked values return to vanilla no later than the next valid writer pass (≤1024 frames). Force a writer exception: values restore in the same pass, the modifier disables, and Park/Core keep running. | PASS (user-confirmed 2026-10-04) |
| C604 Save/load | With the modifier active, save; inspect the save in a mod-less game: vanilla coverage values (no residue). Load with the mod: no doubled factor; the factor returns only after the next native Park pass. After any save (including autosave) vanilla coverage shows until the next native pass (≤1024 frames); this is expected. | PASS (user-confirmed 2026-10-04) |
| C605 Ownership and deletion | Move the shared building's primary owner to the other Park (or suspend the owner): the old factor stays at most until the next native pass (≤1024 frames) and is gone after it and the new owner's factor applies only to a fresh baseline. Delete or destroy a modified building and delete a Park: no errors, no stale writes. | PASS (user-confirmed 2026-10-04) |
| C606 Regression and UI | Re-run Park finance, Quality/Progression, the C5 festival and Industrial C4 support with the modifier on; en-US/ja-JP Finances tab has no raw keys; a failed coverage-module registration leaves Park gameplay running. | PASS (user-confirmed 2026-10-04) |

# Phase 3 C2 — Dedicated Area System headquarters and gate assets

Status: **NOT RUN.** Automated tests cover the exact-identifier contract, provider ownership, activation matrix, content-missing summaries and old-save link preservation; none of the cases below has been performed in game. Importing, authoring the Gate passage and packaging the three assets (see `assets/area-system/README.md`) is a prerequisite and has not been done either. Do not release C2 before the `AS_*.cok` package exists and these cases pass.

| Case | Procedure and expected result | Result |
| --- | --- | --- |
| C201 Package discovery | Install the packaged assets and load a city. The log lists `AS_ParkHeadquarters`, `AS_ParkGate` and `AS_IndustrialHeadquarters` as registered with no rejected/missing warning; all three appear in the asset menu and can be placed. | NOT RUN |
| C202 Park activation matrix | In a Park Area test: nothing; `AS_ParkHeadquarters` only; `AS_ParkGate` only; both. Try a Park Maintenance building, a park/leisure building and a building outside the area as HQ/Entrance. Expected: Only both dedicated assets activate; every other candidate is rejected and the assignment stays pending. The summary names the missing asset. | NOT RUN |
| C203 Industrial activation | Assign `AS_IndustrialHeadquarters`; try an office, factory, warehouse and the Park HQ asset. Vacate nearby companies. Expected: Only `AS_IndustrialHeadquarters` is accepted; Production/Logistics never count it; the Area stays Active. | NOT RUN |
| C204 Anchor lifecycle | Delete, move outside the boundary and replace each required anchor; then reassign it. Expected: The Area suspends with the matching summary and reactivates on reassignment; Level/XP, finance, policies and history are unchanged. | NOT RUN |
| C205 Gate passage | Connect external pedestrian Paths to both ends of `AS_ParkGate`'s authored passage. Expected: Citizens can walk continuously through the gate. No pathfinding hook exists in code; visitors are not forced through the gate. | NOT RUN |
| C206 Old saves | Load a save with a Foundation office HQ, a Park Maintenance HQ and a Park that relied on a Path entrance. Expected: Areas load Suspended; old links are listed as "Not eligible" with Remove; nothing is auto-converted. StableId, Level/XP, finance, history, policies and Industrial company ownership survive. Assigning the dedicated assets reactivates the same Areas. | NOT RUN |
| C207 Missing content | Deploy with `-p:AllowMissingAreaContent=true` (a normal deploy fails without the package) and load without the asset package; then reinstall it and reload. Expected: No crash; Areas are Suspended with the "asset not installed" summary/warnings; no vanilla or Path fallback is accepted. After reinstalling, the prefabs are rediscovered and can be assigned. | NOT RUN |
| C208 Model rule and UI | Inspect the placed models; check en-US/ja-JP selected panels, assignment hints and asset descriptions. Expected: Models carry only building-fixed details (see the known streetlight/bollard limitation in `assets/area-system/README.md`). No raw keys and no Park Maintenance/office/path-entrance wording remain. | NOT RUN |

# Phase 3 D2 — Reference Factory (existing vanilla resources)

Status: **NOT RUN / BLOCKED.** No factory source model was supplied, so `AS_FurnitureFactory` has not been authored, imported
or packaged. The cases below apply once it exists (`assets/industrial-reference-factory/README.md`).

| Case | Procedure and expected result | Result |
| --- | --- | --- |
| D201 Import and contract | Import with the Signature Building preset: Industrial non-office zone, Allowed Manufactured = Furniture only, nothing sold/stored/residential, building-fixed model elements only. After loading a city the log shows `Area System content facts AS_FurnitureFactory manufactured=512 ... signature=True industrialZone=True officeZone=False` and no rejection. | NOT RUN |
| D202 Vanilla company and process | Place the factory inside an Industrial Area. A vanilla Furniture company moves in; its recipe (inputs and amounts) is the vanilla one; company resources (Timber in, Furniture out) change through vanilla trade only. | NOT RUN |
| D203 Observation | Production tab: the process row shows the vanilla recipe and measured rate, Furniture appears in resource rows, bottlenecks and comparison behave as for a vanilla Furniture factory. A vacant factory counts as a producer facility without invented output. | NOT RUN |
| D204 Overlap and manual ownership | Put the factory in two overlapping Industrial Areas and assign its company manually to the non-primary Area: production, jobs and support follow the resolved owner exactly as for vanilla companies. | NOT RUN |
| D205 Save/load, removal, mismatch | Save/load with the factory placed: nothing about the prefab is saved by Area System and it is rediscovered. Remove the package and load: the factory is gone, Areas, vanilla companies and observation are unaffected, and no missing-content warning names it. Re-import with a different manufactured resource: the content warning reports a rejected candidate and no role is granted. | NOT RUN |
| D206 Deletion and regression | Bulldoze the factory: no errors; the company leaves through vanilla rules. Re-run Industrial v1/B2/C4 and D1 contracts with the factory present. | NOT RUN |

Do not change these Results to PASS without direct game evidence.
