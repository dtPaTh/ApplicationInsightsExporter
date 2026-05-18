# Changelog

## 1.2.0

### Added
- `AppExceptions` and `AppTraces` telemetry types are now mapped and forwarded as OTLP log records (previously skipped). Exceptions follow the OTel exception semantic conventions (`exception.type` / `exception.message` / `exception.stacktrace` attributes, severity ERROR, stack assembled from the AI `Details[].parsedStack` frames including inner exceptions). Traces map `Message` to the log body and `SeverityLevel` (0–4) to the corresponding OTel severity (DEBUG / INFO / WARN / ERROR / FATAL). Both carry the originating `OperationId` as the log's `trace_id` and `ParentId` as the `span_id` so they correlate with existing spans in the backend.
- `OTLP_TRACES_ENDPOINT` and `OTLP_LOGS_ENDPOINT` settings for configuring traces and logs targets independently. When `OTLP_ENDPOINT` is set to a bare base URL (no `/v1/traces` or `/v1/logs` suffix), the forwarder now also derives the logs endpoint from it automatically, enabling logs forwarding without an extra setting. Setting `OTLP_ENDPOINT` to a full traces URL keeps the previous traces-only behavior.
- `OTLP_TRACES_HEADER_AUTHORIZATION` and `OTLP_LOGS_HEADER_AUTHORIZATION` settings for configuring the `Authorization` request header per signal. `OTLP_HEADER_AUTHORIZATION` continues to work as a shared fallback applied to both signals; the per-signal setting overrides it when present. Each signal now flows through its own named `HttpClient`, so traces and logs can target backends with different auth tokens.

### Changes
- Retargeted forwarder to .NET 10. **Function App stack must be set to .NET 10 isolated before deploying.**
- Updated OTLP proto to v1.10.0 (`InstrumentationLibrary*` → `ScopeSpans`/`InstrumentationScope`, namespace `Opentelemetry.*` → `OpenTelemetry.*`). Wire-compatible.
- Bumped dependencies: `Google.Protobuf` 3.17.2 → 3.34.1, `Microsoft.Azure.Functions.Worker` 2.51.0 → 2.52.0, `Azure.Identity` 1.17.1 → 1.21.0.
- **Breaking:** Public library API `ApplicationInsights2OTLP.Convert.FromApplicationInsights(string)` now returns `(ExportTraceServiceRequest Traces, ExportLogsServiceRequest Logs)` and dispatches each record to the trace or log side based on telemetry type in a single parse + iteration pass. The previous signature (returning `ExportTraceServiceRequest` only) and the separate `LogsFromApplicationInsights` method are gone — callers need to destructure the tuple (e.g. `var (traces, logs) = conv.FromApplicationInsights(json);`) or access `.Traces` / `.Logs`.

### Fixes
- `AppRequests` and `AppDependencies` records with `Success: false` now ship with OTLP `Span.Status.Code = Error` (previously `Unset`). Backends that distinguish Error from Unset — Dynatrace and others — were rendering failed requests as successful spans because the failure signal from Application Insights was being dropped during conversion. `Success: true` continues to leave the status `Unset` (OTel only allows `Ok` for operator-validated success, which AI does not provide). Dashboards and alerts keyed on span error status will start seeing the real failure counts post-upgrade.

## 1.1.1

### Added
- Forwarder identifies itself on every exported trace via the resource attribute `appinsightsforwarder.useragent`, which carries the same string sent as the OTLP request `User-Agent` (e.g. `ApplicationInsightsForwarder/1.1.1 (+https://github.com/dtPaTh/ApplicationInsightsExporter)`).

### Changes
- `User-Agent` request header version token changed from `major.minor` (e.g. `1.1`) to full semver (e.g. `1.1.1`). Downstream consumers pinning the exact UA string will need to update.
- Unsupported telemetry types (`AppExceptions`, `AppTraces`, etc.) are now explicitly skipped with a debug log instead of falling through and throwing on the missing `DurationMs` field. Previously, a single unsupported record in an EventData batch would crash conversion of the whole batch (all sibling records lost) and produce an error log; the new behavior preserves the rest of the batch and surfaces the skip only at debug level.

### Fixes
- Fixed `appinsights.target` and `appinsights.source` attributes being swapped — they now reflect the AI `Target` and `Source` fields as defined by the spec. Downstream queries/dashboards that read these attributes from this forwarder may need to be updated.

## 1.1.0
### Added
- Added configuration and telemetry conversion tests
- Improved logging of errors processing invalid events and enhanced debug-logs 

### Changes
- Updated dependencies
- Moved releasenotes into changelog.md
- Removed ApplicationInsightsForwarder project (with support for in-process functios on .NET 6),
defaulting to ApplicationInsightsForwarderWorker (.NET 8, isolated-worker)

### Fixes
- Fixed configuration issue creating an invalid OTLP endpoint

## 1.0.0

### Added 
- A new forwarder function based on .NET 8 isolated worker model. 

### Changes
- Updating attribute mapping to better fit OpenTelemetry semantic conventions. 


## 0.9.0

### Changes
- Swichting from OTLP/HTTP json format to OTLP/HTTP binary format. 

## 0.1.0
### Added
- Initial release supporting AppDependency, AppRequests mapped and forwarded to OTLP/HTTP JSON


