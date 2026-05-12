# Changelog

## 1.1.1

### Added
- Forwarder identifies itself on every exported trace via the resource attribute `appinsightsforwarder.useragent`, which carries the same string sent as the OTLP request `User-Agent` (e.g. `ApplicationInsightsForwarder/1.1.1 (+https://github.com/dtPaTh/ApplicationInsightsExporter)`).

### Changes
- `User-Agent` request header version token changed from `major.minor` (e.g. `1.1`) to full semver (e.g. `1.1.1`). Downstream consumers pinning the exact UA string will need to update.

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


