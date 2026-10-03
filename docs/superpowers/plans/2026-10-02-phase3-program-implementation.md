# Phase 3 Program Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the dedicated Area System Headquarters/Entrance asset integration, advisory Industrial Area supply contracts, and custom-factory content that uses only verified vanilla resources.

**Architecture:** Phase 3 builds on the semantic registry and resolved Industrial ownership from Phases 1/2. C2 replaces the Foundation-era temporary vanilla/path Headquarters and Entrance sources with three required Area System assets registered through the semantic registry; D1 adds persisted Area-to-Area agreements but remains observational/advisory; D2 adds asset/content using native `IndustrialProcessData` and existing `Resource` enum values without special-case scoring.

**Tech Stack:** C# 9, Unity ECS, Phase 1 semantic registry/history, Phase 2 ownership, CS2 1.6.2f1 asset/prefab pipeline, NUnit/NUnitLite, React/Vitest.

**Spec:** `docs/superpowers/specs/2026-10-02-headquarters-entrance-and-post-wave-a-roadmap-design.md` Phase 3 and `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md` §D

## Global Constraints

- Start only from latest `main` containing completed Phase 2.
- C2 introduces three authoritative built-in assets: `AS_ParkHeadquarters`, `AS_IndustrialHeadquarters`, and `AS_ParkGate`.
- Every Area System-owned model/prefab identifier added by C2 starts with `AS_` to avoid collisions with future base-game/DLC assets.
- After C2, ordinary office buildings no longer satisfy Industrial Headquarters, Park Maintenance buildings no longer satisfy Park Headquarters, and derived dedicated-Path crossings no longer satisfy Park Entrance activation.
- Existing Area StableId, Level/XP, finance, history and ownership state are preserved; old Headquarters/Entrance links are not auto-converted and do not satisfy activation after C2.
- `AS_ParkGate` must contain an editor-authored pedestrian connection through the gate with connectable/snap-compatible ends for external pedestrian paths; do not patch citizen pathfinding.
- D1 does not intercept routing, transfer requests, pathfinding or market prices.
- D2 uses only existing vanilla `Resource` enum values and valid native process data.
- Custom content must participate through generic semantic/process observation, not hard-coded score exceptions.
- 3D asset work follows the project rule: do not author benches, plants, streetlights or other replaceable props; model only building-fixed elements.

## Review Focus

- Existing save using office/Park Maintenance/path fallback reaches C2: Area state remains intact but the Area is Suspended until the dedicated AS-prefixed asset is assigned.
- Required C2 content missing or removed: no fallback asset/path is silently accepted and unrelated Areas/state still load safely.
- `AS_ParkGate` connects to external pedestrian paths and allows pedestrian traversal without any runtime pathfinding hook.
- Asset upgrade changes runtime prefab Entity: the built-in provider rediscovers exact AS-prefixed authored content and re-registers semantics for the new load generation.
- Future DLC introduces similarly named Headquarters/Gate assets: exact `AS_` identifiers prevent them from being mistaken for Area System content.

---

## Execution order

1. C2 dedicated Area System HQ/Gate asset/content integration
   - `docs/superpowers/plans/2026-10-02-phase3-park-gate-asset-integration-implementation.md`
2. D1 Area-to-Area supply contracts
   - `docs/superpowers/plans/2026-10-02-phase3-industrial-supply-contracts-implementation.md`
3. D2 custom factory / existing-resource content
   - `docs/superpowers/plans/2026-10-02-phase3-custom-factory-content-implementation.md`

Each is a separate PR/release-risk boundary.

## Phase 3 completion gate

- [ ] `AS_ParkHeadquarters`, `AS_IndustrialHeadquarters`, and `AS_ParkGate` pass the verified 1.6.2f1 asset import/content pipeline.
- [ ] C2 semantic registration makes those three prefabs the only valid Park HQ, Industrial HQ, and Park Entrance sources.
- [ ] Gate pedestrian-path connection and external path snapping are verified in game.
- [ ] Old office/Park Maintenance/path Entrance fallback no longer activates Areas after C2, while existing Level/XP/finance/history remain intact.
- [ ] Supply contracts remain advisory and bounded in large Industrial Area counts.
- [ ] At least one custom factory/content asset passes native importer/runtime observation using only existing resources.
- [ ] No new Resource enum, global economy rewrite or pathfinding hook appears in source guards.
- [ ] Runtime smoke checklist and execution records updated before Tourism work starts.
