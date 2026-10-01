# Area policy UI — 1.6.2f1 contracts

Source: the supplied `CS2-UI-reference-1.6.2f1.zip` (`index.js`, beautified for reading) and the supplied 1.6.2f1 `Game.dll` (decompiled with ilspycmd 9.1). Every module path, export and prop below was read from those files. Nothing here was inferred from C# type names alone. Line numbers refer to the beautified bundle and are given only as reading aids.

## Vanilla District policy UI (what we reuse)

| Module path | Export | Used as |
|---|---|---|
| `game-ui/game/components/policy/policy.tsx` | `Policy`, `CompactPolicy` | One policy row (`Policy`) and the collapsed icon (`CompactPolicy`). |
| `game-ui/game/components/policy/policy.module.scss` | `classes` (`padFirst`, …) | `padFirst` on the first expanded row, as vanilla does. |
| `game-ui/game/components/selected-info-panel/shared-components/info-section/info-section-foldout.tsx` | `InfoSectionFoldout` | The expandable section shell used by the vanilla `PoliciesSection`. |
| `game-ui/common/foldout/themes/info-section-foldout.module.scss` | `classes` (`disableActiveState`) | Applied while the pointer is over a compact policy, as vanilla does. |
| `game-ui/game/components/selected-info-panel/selected-info-sections/shared-sections/policies-section/policies-section.module.scss` | `classes` (`compactPolicies`, `label`) | Header classes of the vanilla section. |
| `game-ui/common/focus/auto-navigation-scope.tsx` | `AutoNavigationScope` | Wraps the expanded rows, as vanilla does. |
| `game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx` | `InfoRow` | Section title row and the per-policy status/upkeep sub-row (already used by the area section). |

### `Policy` / `CompactPolicy` props (index.js ≈108036–108140)

- `Policy({policy, onAdjust, className})` and `CompactPolicy({policy, onAdjust, onMouseOver, onMouseLeave})`.
- `policy` is the C# `Game.UI.InGame.UIPolicy` JSON (`UIPolicy.Write`): `{id, icon, entity, active, locked, uiTag, requirements, data}`. `data` is the slider description or `null`.
- Title and description are localized as `Policy.TITLE[${id}]` and `Policy.DESCRIPTION[${id}]` (class `yc` builds `${prefix}[${hash}]`, index.js ≈21569, `hash: e.id`).
- The checkbox button is disabled only when `locked`. `onAdjust(entity, !active, value)` is called on toggle.
- Both components keep `useState(policy.active)` and never resync it from props. The mod therefore keys each component by policy ID + desired state + blocked state, so an authoritative C# change (such as the old policy of a conflict swap) remounts it.
- The tooltip is `UnlockingTooltip` (`game-ui/game/components/progression/unlocking-tooltip/unlocking-tooltip.tsx`). It lists `requirements` only when `locked` and `requireAll.length + requireAny.length > 0`.
- Requirement items are rendered through `game-ui/game/components/progression/requirement-field/requirement-field.tsx` by `__Type`. `prefabs.PrefabUnlockedRequirement` (enum value at index.js ≈38042) renders `{locked, labelId}` as an OpenLock row with `LocalizedString id=labelId`. The mod uses only this type, with its own localization keys:
  - `CS2AS.Policy.Requirement.Level1` … `Level5`
  - the C# unavailable reason key.

### Vanilla `PoliciesSection` (index.js ≈108327)

The vanilla section cannot be reused as a whole. It hardcodes `onAdjust: hw`, which is the vanilla `policies.setPolicy(Entity, bool, float)` trigger. That trigger changes vanilla District/city policy components and must not touch an Area.

The mod therefore reproduces its composition with the same modules:
- `InfoSectionFoldout` with `disableFocus`, `initialExpanded = policies.length < 3`, `expandFromContent`, and `onToggleExpanded`.
- Header when expanded: an uppercase `InfoRow` titled `SelectedInfoPanel.POLICIES`, which is the same vanilla key.
- Header when collapsed: `CompactPolicy` icons inside `compactPolicies`.
- Children: `Policy` rows inside `AutoNavigationScope`.

The only change is `onAdjust`, which sends the mod's guarded trigger.

## Section placement

`SelectedInfoUISystem.AddMiddleSection` and the `selectedInfoSectionComponents` export of `game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx` are the existing, already-proven extension path used by `AreaInfoSection`.

Policies are a second middle section, `CS2AreaSystem.Core.UI.AreaPolicyInfoSection`, registered right after `AreaInfoSection`. This matches the vanilla District panel, where policies are their own section below the district section. There is no floating window.

The section is hidden when the Area type has no policies (Tourism).

## Data and actions

- C# `AreaPolicySectionView` publishes `{stableId, policies[]}`.
- Each `AreaPolicyView` has `policyId, nameKey, descriptionKey, icon, minLevel, desiredEnabled, effective, locked, suspended, effectAvailable, reasonKey, estimatedDailyUpkeep`. Order: common policies first, then module policies by `PolicyId`.
- React maps these flags onto the vanilla props:
  - `active = desiredEnabled`.
  - `locked = !desiredEnabled && (locked || !effectAvailable)`, so a configured policy can always be switched off.
  - `requirements` are built only when the toggle is blocked.
- React computes no unlock, conflict, effective or upkeep state.
- The per-policy sub-row shows the C# status/reason and the C# estimated upkeep.
- Toggle: `cs2AreaSystem/setPolicyEnabled(stableId, policyId, "1"|"0")`. This is the same three-string `TriggerBinding` shape as the existing `moduleAction`.
- C# applies the action only when all of these hold:
  - The selection guard passes (`AreaUIActionGuard.Matches`: published, cached and live selection plus stable ID).
  - The arguments parse strictly (`AreaPolicyToggleRequest`).
  - `AreaPolicySystem.TrySetDesired` passes. It re-validates the Area, lifecycle, policy ID, Area type, current Level, conflict transition and effect availability.

## Fallback

If `Policy` or `InfoSectionFoldout` cannot be resolved, the section renders the vanilla `InfoSection`/`InfoRow` (or the existing plain fallback) with an On/Off `Button` per policy. That fallback uses the same flags, sub-row and trigger.

The section is wrapped in `PanelBoundary`, so a render failure hides only the policy section.

## Not verifiable here

The visual result in the game, the tooltip placement, the gamepad focus behavior and the exact look of the per-policy sub-row next to vanilla rows need a 1.6.2f1 runtime check. They are listed in `docs/verification/area-policy-system-runtime.md`.
