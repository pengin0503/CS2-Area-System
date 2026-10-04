# AI context index

This is navigation guidance; current specs and current code remain authoritative.

Normal loading flow:

```text
CLAUDE.md
  -> docs/ai-context/INDEX.md
      -> smallest relevant domain map(s)
          -> targeted spec section(s)
          -> targeted source file(s)
          -> targeted test file(s)
```

| Task boundary | Load |
| --- | --- |
| Shared area lifecycle, membership, modules, finance, save or UI payload | `docs/ai-context/core.md` |
| Park module observations, controls and panel | `docs/ai-context/park.md` |
| Industrial observations, scoring and panel | `docs/ai-context/industrial.md` |
| Tourism module registration and boundary contracts | `docs/ai-context/tourism.md` |
| React presentation, bindings, localization or UI payload contracts | `docs/ai-context/ui.md` |

Load a second map only when the task crosses its boundary; state the dependency that justifies it.
Follow relevant current specs rather than duplicating requirements into maps.

Archived plans and verification history are on-demand for historical/regression questions, not default loading.
Large generated fixtures, `UI/src/__fixtures__/` and `UI/package-lock.json` are on-demand for directly relevant contracts/dependencies. License text is on-demand for licensing work.
