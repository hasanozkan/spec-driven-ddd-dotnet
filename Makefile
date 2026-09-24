.PHONY: build check test format run contracts-update conformance

build:
	dotnet build -warnaserror

# Everything CI runs on the code: format, build with warnings as errors, and
# the tests — which include the architecture rules, the trace, the policy
# mirror and the contract snapshot.
check:
	dotnet format --verify-no-changes
	dotnet build -warnaserror
	dotnet test --no-build

test:
	dotnet test

format:
	dotnet format

run:
	dotnet run --project src/Library.Api --urls http://localhost:8000

contracts-update:
	UPDATE_CONTRACTS=1 dotnet test --filter "FullyQualifiedName~openapi"

# Same behaviour as the Python implementation: its smoke test, unchanged, against this API.
conformance:
	curl -fsSL https://raw.githubusercontent.com/hasanozkan/gitops-reference/main/scripts/smoke.sh -o /tmp/smoke.sh
	bash /tmp/smoke.sh http://localhost:8000
