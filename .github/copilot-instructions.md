# Copilot instructions for this repository

## Build, test, and run commands

Use solution-level commands from the repository root:

```bash
dotnet restore ./src
dotnet build ./src --configuration Release
dotnet test ./src --no-build
```

Run the CLI tool locally:

```bash
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- move <source> <target> (--connection-string "<service-bus-connection-string>" | --namespace "<service-bus-namespace>") [--success-settlement complete] [--failure-settlement abandon] [--continue-on-failure] [--dry-run] [--limit 1000] [--workers 8] [--receive-batch-size 25] [--prefetch-count 0] [--client-id-prefix "DLQMove"]
```

Single-test command (when test projects exist):

```bash
dotnet test ./src --filter "FullyQualifiedName~Namespace.ClassName.TestName"
```

## High-level architecture

This repository contains a single .NET console tool (`src/Azure.ServiceBus.Tools.Asbctl`) with command-specific modules (`move`, `purge`, `inspect`, `list`, `schedule`, `expedite`) for Service Bus operations.

The end-to-end flow is:

1. `Program.cs` parses CLI arguments with `CommandLineArgumentsParser`.
2. `ServiceBusAdminClient` retrieves source-side runtime message count for startup/progress display.
3. `Program.cs` starts a `ConsoleUI` progress loop and launches `MaxConcurrency` worker tasks.
4. Each worker creates a `ServiceBusMessageMover`, receives from configured source/subqueue, clones each message (`MessageCloner`), sends to configured target, and applies configured settlement behavior.
5. Workers report per-message success/failure via `FluentResults.Result`; totals are aggregated with `Interlocked` and rendered by `ConsoleUI`.

## Key codebase conventions

- Prefer top-level program flow in `Program.cs` and keep operational logic in small focused classes (`ServiceBusMessageMover`, `ServiceBusAdminClient`, `ConsoleUI`, `MessageCloner`).
- Argument style is positional-first (`move <source> <target>`) with optional aliases (`--source`, `--target`) and control flags (`--workers`, `--receive-batch-size`, `--prefetch-count`, `--success-settlement`, `--failure-settlement`, `--continue-on-failure`, `--dry-run`, `--limit`, `--client-id-prefix`). Use Azure Service Bus native paths only: queue (`orders`), subscription (`legacy/Subscriptions/orders-sub`), and source subqueue suffixes (`/$DeadLetterQueue`, `/$Transfer/$DeadLetterQueue`). Keep help text, parser logic, and docs aligned.
- Authentication supports either `--connection-string` or `--namespace` (Azure credential via `DefaultAzureCredential`). Keep auth parsing and client construction behavior aligned across parser, `Program.cs`, `ServiceBusMessageMover`, and `ServiceBusAdminClient`.
- Concurrency defaults and limits come from `Constants` (`DefaultMaxConcurrency`, `MaxAllowedConcurrency`, `DefaultIdentifier`) and should not be duplicated elsewhere.
- Message replay must preserve body/system properties and application properties via `MessageCloner.Clone` before re-sending.
- Cancellation behavior is part of expected UX: Ctrl+C cancels via `CancellationTokenSource`, worker loops stop gracefully, and summary output is still printed.
- Keep parser, receiver creation, sender creation, and admin-count paths consistent with the same `SourceEndpoint`, `TargetEndpoint`, and `MessageMoverBehavior` model.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
<!-- SPECKIT END -->
