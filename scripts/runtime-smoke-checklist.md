# Area Core — Cities: Skylines II 1.6.2f1 runtime verification

Status: **NOT RUN**. Start a fresh 1.6.2f1 session with an official-toolchain build. Record game version, save name and observations in a local test log; do not commit save files or machine paths. Automated tests cannot establish these results.

1. Areas menu appears in the bottom construction UI without editing shipped UI files.
2. Park, Industrial, Tourism selections activate native AreaToolSystem.
3. Create, vertex-edit, complete, re-edit, and delete each managed area type. Stable ID/type survive edits; boundary changes do not clear vanilla buildings/props. Park entrance absent => Suspended; assign an existing building inside the Park as Entrance => Active; Tourism completes => Active.
   - Issue #1: completed Space areas are not raycast by the vanilla default tool. Open **Managed areas** after leaving the draw tool and after a load, pick each area and verify that the vanilla selected-info panel opens with the area section (type, status, entrance/headquarters, progress, Edit/Delete) below the vanilla header, the camera moves to it, renaming via the vanilla header and locate via the vanilla actions work, and the section refreshes immediately after assignment, cancel and lifecycle changes. Check the list/hint placement does not overlap vanilla UI at common resolutions, and that the vanilla panel still works if the area section fails.
   - Issue #13: Edit from the area section must use normal vertex manipulation, not start a polygon redraw seeded with two original vertices. Cancel must preserve the original geometry, stable ID and anchor/progress; it must not switch to an unrelated Object Tool placement mode. For nearby/overlapping areas, verify the native pointer targets the intended boundary.
4. Build a Park inside a Tourism Area and verify both memberships exist. Confirm native overlap notifications are warnings and completion succeeds. Check warning/icon/ECB ordering using keyboard/mouse and gamepad controls.
5. Create overlapping Park Areas and verify exclusive membership primary owner is the smallest persisted stable ID, including after load. Alongside permitted overlap, try an invalid/self-intersecting boundary and an unpurchased city tile: their native errors must still prevent completion. Check that vanilla Space/other Area conflicts and vanilla asset tools retain their original behavior.
6. Create Industrial Area: no HQ => Suspended; assign vanilla building inside it => Active; delete HQ => only its Industrial Area Suspended (progress unchanged); reassign => Active.
7. Save and reload; stable IDs/type/lifecycle/settings/progress/anchor remapping remain and membership rebuild count increments exactly once for load. Include a schema-v1 TypeId=9999 fixture: identity preserved and ModuleUnavailable.
8. Create/move/delete one candidate building and verify incremental counters change without FullRebuildCount increasing. Resize/shrink an Area and verify old memberships disappear and new candidates are found.
9. Temporarily break/disable the UI bundle and verify the save still loads and area simulation/state remains intact.
10. Confirm normal roads, paths, vegetation, and vanilla assets remain in their original menus rather than being duplicated.
11. Start the game with the game language set to ja-JP and then en-US, open the Areas menu and a managed area in the selected-info panel, then exit the game. Menu, asset and panel text must be localized (no raw `Assets.NAME[...]`/`CS2AS.*` keys), the Areas menu/Area Types/three asset icons must render, and `Logs/CS2AreaSystem.log`/`Player.log` must contain no `Area menu localization unavailable`, `Error disposing mod CS2AreaSystem` or `ResourceHandler: Invalid host locations map` errors (issues #15, #16). On 1.6.2f1 no `Area icon ... unavailable` or `Game UI resources could not be verified` warning may appear; if one does, the runtime icon lookup (#8) must be re-checked.
12. Open the Areas toolbar menu in a fresh session and after loading a save: the **Area Types** tab (with icon) and the Park/Industrial/Tourism assets must be listed. Hovering the menu button, the tab and each asset must show a localized tooltip. Then open **Options → CS2 Area System**: every option has a localized label/description; toggling each option (list visibility, camera focus, panel side, delete confirmation, managed overlap) takes effect without restarting and persists after restarting the game (`Settings/ModsSettings/CS2AreaSystem/CS2AreaSystem.coc`).

Diagnostic access: ECS systems expose `AreaMembershipSystem.FullRebuildCount`, `IncrementalMemberUpdateCount`, `IncrementalAreaUpdateCount`, `Index.GetAreas(member)`, `Index.GetMembers(stableId)` and `AreaLoadCoordinatorSystem.RebuildPending`. Use the mod/debug tooling; there is no diagnostics UI in v1.

# Park Area v1 — Cities: Skylines II 1.6.2f1 runtime verification

Status: **NOT RUN — pending runtime verification.** None of the items below has been executed in the game. The automated evidence (NUnit models/state/contracts, Vitest panel tests, net8 API-check build against the supplied 1.6.2f1 DLLs) does not establish any of them. Record game version, save, city size and observations for each item; do not commit saves.

Diagnostic access: `ParkRuntimeSystem.Store` (per-Park landscape/facility/quality/visitor snapshots), `ParkVisitSystem.Tracker` (`SessionCount`, `ActiveCount(id)`), `ParkVisitSystem.ObservedLastUpdate`, `ParkLandscapeSystem.ScanCount`, plus the Park tabs in the selected-info panel.

1. Draw a Park Area with no Entrance → **Suspended**; Overview shows the NoEntrance warning; Level/XP rows show Level 1 / 0.
2. Assign an eligible Entrance (a park, leisure or attraction building inside the area) → **Active**. Try an unrelated building (house, office, factory) and a building outside the area: both must be rejected and the assignment stays pending.
3. Add a second valid Entrance (Add entrance) → remains Active, the entrance list shows both, Accessibility rises (more for entrances far apart than for two adjacent ones).
4. Remove the first Entrance with its Remove button (or demolish it) → remains Active while the second survives.
5. Remove/demolish all Entrances → **Suspended**; Level, XP, lifetime visitors, admission settings, budget and statistics are unchanged; no management cost is charged and no XP is earned while suspended.
6. Add real park/leisure/attraction buildings inside → Facility categories, Attractiveness and Variety change within a few in-game minutes. Unrelated buildings inside must not change them.
7. Place trees, benches/tables, statues/fountains, lamps and footpaths → Landscape tab categories change after the next scan; `AreaMembershipSystem.Index.GetMembers(id)` still contains buildings only (no trees/props).
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
20. Large city profiling (100k+ population, several Parks): no noticeable frame-time increase; `ParkVisitSystem` processes one of 16 citizen groups every 64 simulation frames and never scans all citizens × all Parks; `ParkLandscapeSystem` scans at most one Park every 8 frames.

Additional UI checks: all five Park tabs render in ja-JP and en-US without raw `CS2AS.*` keys; the vanilla slider (or the +/− fallback) sends budget/fee changes only for the selected Park; a failing Park tab hides only that tab content while Type/Status/Edit/Delete keep working. Park Policies/ordinances and Events/Festivals are intentionally absent in v1.
