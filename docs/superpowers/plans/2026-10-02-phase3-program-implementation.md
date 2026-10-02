# Phase 3 Program Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the optional Park Gate content integration, advisory Industrial Area supply contracts, and custom-factory content that uses only verified vanilla resources.

**Architecture:** Phase 3 builds on the semantic registry and resolved Industrial ownership from Phases 1/2. C2 adds optional explicit Park Entrance content without replacing path-crossing entrances; D1 adds persisted Area-to-Area agreements but remains observational/advisory; D2 adds asset/content using native `IndustrialProcessData` and existing `Resource` enum values without special-case scoring.

**Tech Stack:** C# 9, Unity ECS, Phase 1 semantic registry/history, Phase 2 ownership, CS2 1.6.2f1 prefab/content pipeline, NUnit/NUnitLite, React/Vitest.

**Spec:** `docs/superpowers/specs/2026-10-02-headquarters-entrance-and-post-wave-a-roadmap-design.md` Phase 3 and `docs/superpowers/specs/2026-10-02-pre-tourism-park-industrial-expansion-design.md` §D

## Global Constraints

- Start only from latest `main` containing completed Phase 2.
- No dedicated Industrial HQ asset is required; ordinary office buildings remain Industrial Headquarters.
- Park Maintenance remains the baseline Park Headquarters.
- Dedicated Park Gate is optional; existing dedicated-Path entrance evidence must remain valid.
- D1 does not intercept routing, transfer requests, pathfinding or market prices.
- D2 uses only existing vanilla `Resource` enum values and valid native process data.
- Custom content must participate through generic semantic/process observation, not hard-coded score exceptions.
- 3D asset work follows the project rule: do not author benches, plants, streetlights or other replaceable props; model only building-fixed elements.

## Review Focus

- Installing/removing the optional Park Gate content never invalidates a path-based Park.
- Supply contract references an Area deleted after save: contract becomes invalid/inert without resolving to a reused StableId.
- D1 reports “advisory/estimated”, never claims a physical transfer occurred from observed capacity alone.
- Custom factory prefab missing/changed recipe data: observation degrades to unavailable instead of inventing output.
- Content pack removed after save: Core/Industrial state loads without quarantining unrelated Areas.

---

## Execution order

1. C2 optional Park Gate asset/content integration
   - `docs/superpowers/plans/2026-10-02-phase3-park-gate-asset-integration-implementation.md`
2. D1 Area-to-Area supply contracts
   - `docs/superpowers/plans/2026-10-02-phase3-industrial-supply-contracts-implementation.md`
3. D2 custom factory / existing-resource content
   - `docs/superpowers/plans/2026-10-02-phase3-custom-factory-content-implementation.md`

Each is a separate PR/release-risk boundary.

## Phase 3 completion gate

- [ ] Optional Gate install/uninstall/load compatibility verified.
- [ ] Supply contracts remain advisory and bounded in large Industrial Area counts.
- [ ] At least one custom factory/content asset passes native importer/runtime observation using only existing resources.
- [ ] No new Resource enum, global economy rewrite or pathfinding hook appears in source guards.
- [ ] Runtime smoke checklist and execution records updated before Tourism work starts.
