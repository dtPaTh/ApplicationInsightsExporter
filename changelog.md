# Changelog

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


