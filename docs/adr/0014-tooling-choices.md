# ADR-0014: Tooling: .slnx, CPM, xunit.v3, Shouldly, Testcontainers, no MediatR/FluentAssertions/Serilog

- Status: Proposed
- Date: 2026-09-03

## Context and decision
.slnx is the .NET 10 default. MediatR and FluentAssertions moved to commercial licenses; a handler per use case and Shouldly cover the need. Built-in JSON console logging + OpenTelemetry replace Serilog. Two OpenTelemetry packages (Prometheus exporter, StackExchange.Redis instrumentation) only exist as beta and are pinned explicitly. Tests run on Microsoft.Testing.Platform (xunit.v3 4.0 dropped MTP v1; the .NET 10 SDK requires the MTP mode of dotnet test via global.json). coverlet.collector is a VSTest collector and was removed; coverage will come from the MTP code-coverage extension. Gotcha: never pass --nologo to dotnet test in MTP mode, it is forwarded to the test app and produces "Zero tests ran" with exit code 5.

## Consequences
_To be completed when the micro-project that implements it lands._
