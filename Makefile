# Developer entry points. Works with GNU make on Linux/macOS/Git Bash.
# STUB (MP-02): targets are declared; bodies are wired as each micro-project lands.

.PHONY: build test test-unit test-integration run-api run-worker run-mcp compose-up compose-down smoke format pack

build:
	dotnet build Coliseum.slnx -c Release

test:
	dotnet test Coliseum.slnx -c Release

test-unit:
	dotnet test tests/Coliseum.UnitTests -c Release
	dotnet test tests/Coliseum.RegressionTests -c Release

test-integration:
	dotnet test tests/Coliseum.IntegrationTests -c Release

run-api:
	dotnet run --project src/Coliseum.Api

run-worker:
	dotnet run --project src/Coliseum.Worker

run-mcp:
	dotnet run --project src/Coliseum.Mcp

compose-up:
	docker compose -f deploy/compose/docker-compose.yml up --build -d

compose-down:
	docker compose -f deploy/compose/docker-compose.yml down -v

smoke:
	bash scripts/smoke.sh

format:
	dotnet format Coliseum.slnx --verify-no-changes

pack:
	dotnet pack src/Coliseum.Domain -c Release -o artifacts/nuget
	dotnet pack src/Coliseum.Contracts -c Release -o artifacts/nuget
