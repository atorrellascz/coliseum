# ADR-0014: Tooling choices

- Status: Accepted
- Date: 2026-09-03

## Context and decision
- `.slnx` solution format (the .NET 10 default): readable XML, no GUIDs.
- Central Package Management with transitive pinning; a repo-level `nuget.config` with `<clear/>` so every clone
  and CI run resolves from nuget.org only.
- Analyzers at `latest-recommended` with warnings as errors, `EnforceCodeStyleInBuild`, deterministic builds.
- xunit.v3 on Microsoft.Testing.Platform (`global.json` `test.runner`), Shouldly, NSubstitute, Testcontainers.
- No MediatR (a handler per use case needs no library), no FluentAssertions (license change), no Serilog
  (built-in JSON console plus OpenTelemetry logs). Two OpenTelemetry packages (Prometheus exporter, StackExchange.Redis
  instrumentation) only exist as beta and are pinned explicitly.
- Source-generated logging (`[LoggerMessage]`) everywhere: required by CA1873 and cheaper at runtime.

## Consequences
- `dotnet test` must be invoked per project (`--project`) and never with `--nologo` in MTP mode (the flag is
  forwarded to the test app, which exits with code 5 and reports "Zero tests ran").
- coverlet (a VSTest collector) was removed; coverage would come from the MTP code-coverage extension.
- Naming rules are strict: private const/static fields PascalCase, instance fields `_camelCase`; CA1711 (the
  `Queue` suffix) is disabled with a justification because `IBattleQueue` is literally a queue.
- The dependency rule and the composition-only `Program.cs` are enforced by tests, not conventions.
