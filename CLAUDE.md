# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test

```powershell
dotnet build ApplicationInsightsExporter.sln
dotnet test ApplicationInsightsTest/ApplicationInsightsTest.csproj
dotnet test ApplicationInsightsTest/ApplicationInsightsTest.csproj --filter "FullyQualifiedName~ConvertAppRequeste2OTLP"
```

Run the Azure Function locally (requires Azure Functions Core Tools `func`):

```powershell
Set-Location ApplicationInsightsForwarderWorker
func start
```

Local config goes in `ApplicationInsightsForwarderWorker/local.settings.json` — see [readme.md](readme.md) for the template. Required values: `EHConnection` (Event Hub connection string with `EntityPath`), `OTLP_ENDPOINT`, optional `OTLP_HEADER_AUTHORIZATION`.

### Protobuf code generation (Windows-only build step)

[ApplicationInsights2OLTP.csproj](ApplicationInsights2OLTP/ApplicationInsights2OLTP.csproj) has a `PreBuild` target that invokes `protoc.exe` from the `google.protobuf.tools` NuGet package's `tools\windows_x86\` directory to regenerate OTLP message classes from `.proto` files in [ApplicationInsights2OLTP/proto/](ApplicationInsights2OLTP/proto/) into `ApplicationInsights2OLTP/Messages/`. **Building on non-Windows will fail at this step.** If `.proto` files change, the build regenerates `Messages/`; do not edit those generated files by hand.

## Architecture

Three-project .NET solution. Data flow is unidirectional: Azure Event Hub → Worker function → Convert library → OTLP/HTTP endpoint.

### [ApplicationInsights2OLTP/](ApplicationInsights2OLTP/) — conversion library (netstandard2.1)

Pure library, no Azure dependencies. Single public entry point: [`Convert.FromApplicationInsights(string)`](ApplicationInsights2OLTP/Convert.cs) which parses the Application Insights JSON envelope (`{ records: [...] }`) **once** and returns a tuple `(ExportTraceServiceRequest Traces, ExportLogsServiceRequest Logs)`. Each record is dispatched by telemetry type to either the trace side (`AppRequests`, `AppDependencies` → spans via the private `AppendSpan` helper) or the log side (`AppExceptions`, `AppTraces` → log records via `AppendLog`); any other type is skipped with a debug log. Both returned requests are non-null but may be empty (zero spans / zero log records) when the batch contains nothing for that signal — callers check `ResourceSpans[0].ScopeSpans[0].Spans.Count > 0` / `ResourceLogs[0].ScopeLogs[0].LogRecords.Count > 0` before exporting. The per-record loop continues on any single-record failure so a bad record can't drop the rest of the batch.

Key mapping rules in [Convert.cs](ApplicationInsights2OLTP/Convert.cs):
- `operationId` → OTLP `TraceId`, validated against a 32-char hex regex. Invalid trace IDs cause the record to be skipped with a warning (W3C TraceContext must be enabled in the source app).
- `SpanKind` is derived from telemetry type + dependency type: `AppRequests`→Server, `AppDependencies`→Client, except `Az Service Bus Message`→Producer.
- Dependency mapping branches on `DependencyType` (Http / Http (tracked), Backend, Azure Service Bus, Azure Service Bus Message) — each branch parses different attributes from `Name`/`Data`/`Target`.
- Two parallel semantic-convention namespaces live in [src/internal/](ApplicationInsights2OLTP/src/internal/): `ApplicationInsights.SemanticConventions` (for `appinsights.*` attributes) and `OpenTelemetry.SemanticConventions` (for standard OTel attributes). When adding new attribute mappings, prefer OTel conventions; only fall back to the AI namespace for AI-specific concepts.
- `#if DEBUG` adds a `_SimulateRealtime` mode that rewrites timestamps to "now" and generates fresh trace IDs — used only by tests / local sandbox runs.

[ExportRequestContent.cs](ApplicationInsights2OLTP/ExportRequestContent.cs) is a `HttpContent` wrapper that serializes the request as `application/x-protobuf` (OTLP/HTTP binary). The codebase deliberately does NOT use the OTel SDK's exporter to keep dependencies minimal.

### [ApplicationInsightsForwarderWorker/](ApplicationInsightsForwarderWorker/) — Azure Functions host (.NET 8, isolated worker)

[Program.cs](ApplicationInsightsForwarderWorker/Program.cs) wires DI: `ForwarderConfig` (singleton), a single named `HttpClient` (`ApplicationInsightsForwarder.HttpClientName` = `"ApplicationInsightsExporter"`) configured with the `User-Agent` header, and the `Convert` singleton.

[ApplicationInsightsForwarder.cs](ApplicationInsightsForwarderWorker/ApplicationInsightsForwarder.cs) is a single function `ForwardAI` bound to `[EventHubTrigger("appinsights", Connection = "EHConnection")]`. It iterates the batch, converts each event, POSTs traces to `_config.TracesEndpoint` and (when set) logs to `_config.LogsEndpoint`. **Exceptions per message are caught and logged — the loop continues** so one bad record doesn't poison the whole batch.

[ForwarderConfig.cs](ApplicationInsightsForwarderWorker/ForwarderConfig.cs) resolves the OTLP target(s) from up to three endpoint settings:
- `OTLP_TRACES_ENDPOINT` and `OTLP_LOGS_ENDPOINT` (explicit, per-signal) always win when set; the value gets `/v1/traces` or `/v1/logs` appended if missing.
- `OTLP_ENDPOINT` acts as a fallback. When it is a bare base URL (no `/v1/traces` or `/v1/logs`), both traces and logs endpoints are derived from it by appending the respective paths. When it already contains a signal-specific path it only feeds that signal (back-compat for deployments that historically set `OTLP_ENDPOINT=https://host/v1/traces`).
- Traces falls back to `http://localhost:4318/v1/traces` when nothing is configured (with a warning log). Logs stays `null` when nothing yields a logs URL, and `ApplicationInsightsForwarder` skips logs forwarding in that case.

The same three-tier pattern resolves the `Authorization` request header per signal: `OTLP_TRACES_HEADER_AUTHORIZATION` / `OTLP_LOGS_HEADER_AUTHORIZATION` (explicit, per-signal) win when set; otherwise `OTLP_HEADER_AUTHORIZATION` is used as a shared fallback for both. [ApplicationInsightsForwarder.cs](ApplicationInsightsForwarderWorker/ApplicationInsightsForwarder.cs) attaches the resolved header per request (so traces and logs can target backends with different tokens through a single shared `HttpClient`). The values are still read from env vars **once at startup** when `ForwarderConfig` is constructed, so changing them requires a function restart.

### [ApplicationInsightsTest/](ApplicationInsightsTest/) — NUnit tests (.NET 8)

Loads sample JSON from [samples/](ApplicationInsightsTest/samples/) (`AppRequests.json`, `AppDependencies.json`, `Invalid.json`, `Invalid.txt`), runs them through `Convert`, and asserts on the resulting OTLP structure. New telemetry-mapping behaviors should get a corresponding sample + test here.

## Versioning

The forwarder's identity is defined once in [ForwarderInfo.cs](ApplicationInsightsForwarderWorker/ForwarderInfo.cs) as `Name`, `Version`, `Url`, and a derived `UserAgent` string. That single `UserAgent` string drives two runtime markers wired up in [Program.cs](ApplicationInsightsForwarderWorker/Program.cs):
- the OTLP request `User-Agent` header (via `ParseAdd`)
- the resource attribute `appinsightsforwarder.useragent` on every exported span (emitted by [Convert.cs](ApplicationInsights2OLTP/Convert.cs) when constructed with a producer user-agent — the library defaults to omitting it if unset, keeping it generic)

When releasing, update both:
- `ForwarderInfo.Version` in `ForwarderInfo.cs`
- The `## X.Y.Z` heading at the top of [changelog.md](changelog.md)

## Deployment

Single manual-trigger workflow: [.github/workflows/deploy-ApplicationInsightsForwarderWorker.yml](.github/workflows/deploy-ApplicationInsightsForwarderWorker.yml). Requires the target Function App to have `WEBSITE_RUN_FROM_PACKAGE = 1`, the `AZURE_FUNCTIONAPP_PUBLISH_PROFILE` secret, and the `AZURE_FUNCTIONAPP_NAME` repository variable. The legacy in-process `ApplicationInsightsForwarder` project (.NET 6) was removed in 1.1.0 — see [changelog.md](changelog.md).
