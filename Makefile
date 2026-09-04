# Developer entry points. Works with GNU make on Linux/macOS/Git Bash.
# NOTE: dotnet test runs in Microsoft.Testing.Platform mode (global.json). Never pass --nologo to it:
# the flag is forwarded to the test app, which rejects it with exit code 5 and reports "Zero tests ran".
# STUB (MP-02): targets are declared; bodies are wired as each micro-project lands.

.PHONY: build test test-unit test-integration run-api run-worker run-mcp compose-up compose-down smoke format pack

build:
	dotnet build Coliseum.slnx -c Release

test: test-unit test-integration

test-unit:
	dotnet test --project tests/Coliseum.UnitTests -c Release
	dotnet test --project tests/Coliseum.RegressionTests -c Release

test-integration:
	dotnet test --project tests/Coliseum.IntegrationTests -c Release

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
	API_URL=${API_URL:-http://localhost:8080} API_KEY=${API_KEY:-dev-service-key} bash scripts/smoke.sh

format:
	dotnet format Coliseum.slnx --verify-no-changes

pack:
	dotnet pack src/Coliseum.Domain -c Release -o artifacts/nuget
	dotnet pack src/Coliseum.Contracts -c Release -o artifacts/nuget
