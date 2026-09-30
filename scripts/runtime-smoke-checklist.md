# Area Core — Cities: Skylines II 1.6.2f1 runtime verification

Status: **NOT RUN**. Start a fresh 1.6.2f1 session with an official-toolchain build. Record game version, save name and observations in a local test log; do not commit save files or machine paths. Automated tests cannot establish these results.

1. Areas menu appears in the bottom construction UI without editing shipped UI files.
2. Park, Industrial, Tourism selections activate native AreaToolSystem.
3. Create, vertex-edit, complete, re-edit, and delete each managed area type. Stable ID/type survive edits; boundary changes do not clear vanilla buildings/props. Park entrance absent => Suspended; assign an existing building inside the Park as Entrance => Active; Tourism completes => Active.
4. Build a Park inside a Tourism Area and verify both memberships exist. Confirm native overlap notifications are warnings and completion succeeds. Check warning/icon/ECB ordering using keyboard/mouse and gamepad controls.
5. Create overlapping Park Areas and verify exclusive membership primary owner is the smallest persisted stable ID, including after load. Alongside permitted overlap, try an invalid/self-intersecting boundary and an unpurchased city tile: their native errors must still prevent completion. Check that vanilla Space/other Area conflicts and vanilla asset tools retain their original behavior.
6. Create Industrial Area: no HQ => Suspended; assign vanilla building inside it => Active; delete HQ => only its Industrial Area Suspended (progress unchanged); reassign => Active.
7. Save and reload; stable IDs/type/lifecycle/settings/progress/anchor remapping remain and membership rebuild count increments exactly once for load. Include a schema-v1 TypeId=9999 fixture: identity preserved and ModuleUnavailable.
8. Create/move/delete one candidate building and verify incremental counters change without FullRebuildCount increasing. Resize/shrink an Area and verify old memberships disappear and new candidates are found.
9. Temporarily break/disable the UI bundle and verify the save still loads and area simulation/state remains intact.
10. Confirm normal roads, paths, vegetation, and vanilla assets remain in their original menus rather than being duplicated.

Diagnostic access: ECS systems expose `AreaMembershipSystem.FullRebuildCount`, `IncrementalMemberUpdateCount`, `IncrementalAreaUpdateCount`, `Index.GetAreas(member)`, `Index.GetMembers(stableId)` and `AreaLoadCoordinatorSystem.RebuildPending`. Use the mod/debug tooling; there is no diagnostics UI in v1.
