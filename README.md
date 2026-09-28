# azure-servicebus-tools

CLI toolbox for Azure Service Bus operational tasks.

## Build

```bash
dotnet restore ./src
dotnet build ./src --configuration Release
```

## Release process

Releases are tag-driven with semantic versions:

```bash
git tag v1.2.3
git push origin v1.2.3
```

The `Release` workflow will build/test the solution, pack the .NET tool, push it to NuGet (`NUGET_API_KEY` secret), and attach runtime binaries to a GitHub Release.

## Integration tests (Service Bus emulator)

Integration tests are in `Azure.ServiceBus.Tools.IntegrationTests`, tagged with `Category=Integration`, and start the Service Bus emulator via Testcontainers (including the SQL dependency container).

Requirements:

- Docker engine available on the machine running tests.

Run integration tests only:

```bash
dotnet test ./src --filter "Category=Integration"
# or
./scripts/run-integration-tests.sh
```

Run all non-integration tests:

```bash
dotnet test ./src --filter "Category!=Integration"
```

## Tools

`asbctl` - unified Azure Service Bus CLI with commands:

- `move` - move messages between entities.
- `purge` - purge messages from a source entity.
- `inspect` - read properties for queue, topic, or subscription entities.
- `list` - list queues, topics, or subscriptions with key properties.
- `schedule` - schedule queue messages for later processing.
- `expedite` - make scheduled queue messages available immediately.

## Install (recommended)

Install from NuGet as a global .NET tool:

```bash
dotnet tool install --global Cjoergensen.Azure.ServiceBus.Tools.Asbctl
```

Update to the latest version:

```bash
dotnet tool update --global Cjoergensen.Azure.ServiceBus.Tools.Asbctl
```

Install a specific version:

```bash
dotnet tool install --global Cjoergensen.Azure.ServiceBus.Tools.Asbctl --version <version>
```

## Alternative install (GitHub Releases)

If you cannot use NuGet feeds, download a runtime binary archive from GitHub Releases:

- `asbctl-linux-x64.zip`
- `asbctl-linux-arm64.zip`
- `asbctl-win-x64.zip`
- `asbctl-osx-x64.zip`
- `asbctl-osx-arm64.zip`

Show help:

```bash
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- --help
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- <command> --help
```

Enable shell completion:

```bash
source <(asbctl completion bash)
# or
source <(asbctl completion zsh)
```

## Solution layout

The solution is centered around the `asbctl` executable (`Azure.ServiceBus.Tools.Asbctl`) and explicit command modules:

- `Azure.ServiceBus.Tools.Move`
- `Azure.ServiceBus.Tools.Purge`
- `Azure.ServiceBus.Tools.Inspect`
- `Azure.ServiceBus.Tools.Schedule`
- `Azure.ServiceBus.Tools.Expedite`

Shared cross-cutting logic (authentication, endpoint/path parsing, Service Bus client/admin helpers, and shared option parsing) lives in `Azure.ServiceBus.Tools.Shared`.

Common flags are handled consistently across commands:

- Authentication: `--connection-string` or `--namespace`
- Worker/runtime controls (where applicable): `--workers`, `--receive-batch-size`, `--prefetch-count`
- Shared client identity control: `--client-id-prefix`

## Message mover usage

```bash
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- move <source> <target> (--connection-string "<service-bus-connection-string>" | --namespace "<service-bus-namespace>") [--success-settlement <complete|abandon|deadletter|none>] [--failure-settlement <complete|abandon|deadletter|none>] [--continue-on-failure] [--dry-run] [--limit <count>] [--max-idle-minutes <n>] [--workers <n>] [--receive-batch-size <n>] [--prefetch-count <n>] [--client-id-prefix <identifier>]
```

You can also use `--source` and `--target` instead of positional `<source> <target>`, but not both at the same time.

## Purger usage

```bash
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- purge <source> (--connection-string "<service-bus-connection-string>" | --namespace "<service-bus-namespace>") [--continue-on-failure] [--limit <count>] [--workers <n>] [--receive-batch-size <n>] [--prefetch-count <n>] [--client-id-prefix <identifier>]
```

## Entity inspector usage

```bash
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- inspect <entity> (--connection-string "<service-bus-connection-string>" | --namespace "<service-bus-namespace>")
```

`<entity>` supports:

- Queue or topic path: `<entity-name>`
- Subscription path: `<topic-name>/Subscriptions/<subscription-name>`
- Optional subqueue suffixes:
  - `/$DeadLetterQueue` (queue and subscription paths)
  - `/$Transfer/$DeadLetterQueue` (queue paths only)

## List usage

```bash
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- list queues (--connection-string "<service-bus-connection-string>" | --namespace "<service-bus-namespace>") [--output <tsv|json>]
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- list topics (--connection-string "<service-bus-connection-string>" | --namespace "<service-bus-namespace>") [--output <tsv|json>]
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- list subscriptions <topic> (--connection-string "<service-bus-connection-string>" | --namespace "<service-bus-namespace>") [--output <tsv|json>]
```

- `--output <format>` - output format: `tsv` (default) or `json`

## Schedule usage

```bash
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- schedule <source> (--connection-string "<service-bus-connection-string>" | --namespace "<service-bus-namespace>") (--message-id "<id>" | --all) (--for "<hh:mm:ss>" | --until "<yyyy-MM-ddTHH:mm:ssZ>")
```

`<source>` supports queue paths, including subqueue suffixes:

- Queue path: `<queue-name>`
- Queue dead-letter: `<queue-name>/$DeadLetterQueue`
- Queue transfer dead-letter: `<queue-name>/$Transfer/$DeadLetterQueue`

## Expedite usage

```bash
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- expedite <source> (--connection-string "<service-bus-connection-string>" | --namespace "<service-bus-namespace>") (--message-id "<id>" | --all) [--peek-batch-size <n>] [--continue-on-failure]
```

`<source>` supports primary queue path only: `<queue-name>`.

## Path format

- Queue path: `<queue-name>`
- Subscription path: `<topic-name>/Subscriptions/<subscription-name>`
- Source subqueue suffixes:
  - `/$DeadLetterQueue`
  - `/$Transfer/$DeadLetterQueue` (queue sources only)

For mover, target must be a queue/topic entity path only (no subqueue suffix, no subscription path).

## Authentication

Provide exactly one of:

- `--connection-string` (Service Bus connection string)
- `--namespace` (Azure credential auth via `DefaultAzureCredential`)

`--namespace` accepts either:

- `mynamespace`
- `mynamespace.servicebus.windows.net`

## Message mover behavior flags

- `--success-settlement <complete|abandon|deadletter|none>` (default: `complete`, or `none` when `--dry-run` is set)
- `--failure-settlement <complete|abandon|deadletter|none>` (default: `abandon`)
- `--continue-on-failure` to keep processing after individual failures
- `--dry-run` to receive/process without sending to target
- `--limit <count>` to stop after processing a maximum number of messages
- `--max-idle-minutes <n>` keep polling while source runtime count is still non-zero before stopping (default: `2`)

## Purger behavior flags

- `--continue-on-failure` to keep purging after individual failures
- `--limit <count>` to stop after processing a maximum number of messages

## Worker controls (both tools)

- `--workers <n>` parallel workers (default: `8`, max: `32`)
- `--receive-batch-size <n>` receive batch size per worker iteration (default: `25`, max: `500`)
- `--prefetch-count <n>` receiver prefetch count (default: `0`)
- `--client-id-prefix <identifier>` client identifier prefix
  - mover default: `DLQMove`
  - purger default: `SBPurge`

## Examples

```bash
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- move orders orders-replay --connection-string "Endpoint=sb://..." --workers 16 --receive-batch-size 50 --prefetch-count 200
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- move legacy/Subscriptions/orders-sub/$DeadLetterQueue orders --namespace mynamespace --continue-on-failure
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- move orders/$DeadLetterQueue orders --namespace mynamespace.servicebus.windows.net --dry-run --limit 100
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- purge orders/$DeadLetterQueue --namespace mynamespace.servicebus.windows.net --workers 8 --receive-batch-size 50
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- inspect orders --namespace mynamespace
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- inspect orders-events/Subscriptions/replay-sub --connection-string "Endpoint=sb://..."
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- inspect orders-events/Subscriptions/replay-sub/$DeadLetterQueue --namespace mynamespace
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- list queues --namespace mynamespace
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- list topics --connection-string "Endpoint=sb://..." --output json
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- list subscriptions orders-events --namespace mynamespace
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- schedule orders --message-id orders-1 --for 00:30:00 --namespace mynamespace
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- schedule orders --all --until 2026-06-22T08:00:00Z --connection-string "Endpoint=sb://..."
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- expedite orders --message-id orders-scheduled-1 --namespace mynamespace
dotnet run --project ./src/Azure.ServiceBus.Tools.Asbctl -- expedite orders --all --connection-string "Endpoint=sb://..."
```
