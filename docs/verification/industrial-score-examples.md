# Industrial Area v1 — executable calculation examples

**Synthetic.** These rows are semantic-input examples from design spec §12.2. `IndustrialScoreExampleTests` reads [`industrial-score-examples.json`](industrial-score-examples.json), runs the real pure models (`IndustrialWeighting`, `IndustrialProductionModel`, `IndustrialFlowWindow` + `IndustrialLogisticsModel`, `IndustrialEmploymentModel`, `IndustrialSourceNormalization`, `IndustrialPerformanceModel`, `IndustrialProgressionModel`) and asserts these outputs (scores within 1e-5, exact XP). They do **not** show that live 1.6.2f1 company/flow fields carry these values; that is a separate runtime check (smoke checklist item 27, NOT RUN).

Weights: Production = 65% activity + 25% active-producer share + 10% diversity (1/2/3+ meaningful outputs → 50/75/100). Logistics = 50% input + 50% output continuity, `continuity = 100·(1 − affected weighted time / valid weighted time)` over the six-hour window. Employment = 75% aggregate fill + 25% mean facility fill. Efficiency = 70% building efficiency + 30% profitability viability. Performance = 35% Production + 25% Logistics + 20% Employment + 20% Efficiency (one missing → renormalize; two → unavailable). XP operating score = 35% Production + 25% Employment + 20% Logistics + 20% Performance (three of four required), `hourlyXp = clamp(roundAwayFromZero(score/10), 0, 10)`.

| Example | Normalized inputs | Production | Logistics | Employment | Efficiency | Performance | XP score | Hourly XP |
|---|---|---|---|---|---|---|---|---|
| Healthy specialized small district | 3 producers, activity 100, all operating, one output, no flow symptoms over 6 h, all jobs filled, efficiency 100%, profit index 255 | 95 | 100 | 100 | 100 | 98.25 | 97.9 | 10 |
| Equally healthy large district | same proportions, 40 producers × 120 jobs, 6 warehouses, 2 cargo stations | 95 | 100 | 100 | 100 | 98.25 | 97.9 | 10 (scale gates differ) |
| No dedicated warehouse/cargo terminal | healthy small flows, 0 storage, 0 cargo | — | 100 | — | — | — | — | — |
| Partial flow problem | input affected 50% of covered time, output 25% | — | 62.5 | — | — | — | — | — |
| Stopped district with confirmed symptoms | activity 0, 0 of 3 operating, output activity 0, both channels affected 100%, no employees, efficiency 0, profit index −255 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| One unavailable component | activity unavailable; both channels 20% affected; 60 of 100 jobs filled; efficiency 100%, profit index 255 | unavailable | 80 | 60 | 100 | 80 | 72.307692… | 7 |
| Two unavailable components | activity and flow unavailable | unavailable | unavailable | 60 | 100 | unavailable | — | 0 (InsufficientInputs) |

Additional automated cases (in `IndustrialPerformanceModelTests` and `IndustrialProgressionModelTests`): no applicable input channel, one missing Efficiency source, zero jobs, partial observation coverage, invalid/NaN values, multi-tenant/multi-role deduplication.
