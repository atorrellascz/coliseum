# ADR-0014: Tooling: .slnx, CPM, xunit.v3, Shouldly, Testcontainers, no MediatR/FluentAssertions/Serilog

- Status: Proposed
- Date: 2026-09-03

## Context and decision
.slnx is the .NET 10 default. MediatR and FluentAssertions moved to commercial licenses; a handler per use case and Shouldly cover the need. Built-in JSON console logging + OpenTelemetry replace Serilog. Two OpenTelemetry packages (Prometheus exporter, StackExchange.Redis instrumentation) only exist as beta and are pinned explicitly.

## Consequences
_To be completed when the micro-project that implements it lands._
