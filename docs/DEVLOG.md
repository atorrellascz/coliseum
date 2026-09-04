# DEVLOG

One entry per step, newest at the bottom. Public counterpart of the private planning log.

| Date | Step | What | Verification |
|------|------|------|--------------|
| 2026-09-03 | MP-02 | Solution skeleton: 8 src projects, 3 test projects, Central Package Management with pinned versions, analyzers as errors, commented stubs for every planned file, deploy/docs placeholders. | `dotnet restore` OK on all 11 projects; `dotnet build` of the 5 class libraries + Unit/Regression tests: 0 warnings, 0 errors. Hosts compile from MP-06. |
| 2026-09-04 | MP-03 | Domain and battle engine: Result/DomainError, PlayerId/BattleId, Resources, CombatStats, Player aggregate, xoshiro256** + SplitMix64 + FNV-1a seed, BattleRules, BattleEngine (integer math, deterministic), TurnEvent/LootResult/BattleReport. Tests: boundaries, spec examples, scripted rolls, 2,000-battle property test, PRNG known-answer tests, architecture dependency rules, 10 golden reports. Test stack moved to Microsoft.Testing.Platform mode (`global.json`), coverlet dropped. | Domain build 0 warnings; `dotnet test --project`: UnitTests 72/72, RegressionTests 10/10 |
