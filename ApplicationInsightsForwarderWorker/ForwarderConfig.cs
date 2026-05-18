using Microsoft.Extensions.Logging;
using System;

namespace ApplicationInsightsForwarderWorker
{
    public class ForwarderConfig
    {
        private const string DefaultBase = "http://localhost:4318";
        private const string TracesPath = "/v1/traces";
        private const string LogsPath = "/v1/logs";

        public string OTLPEndpoint => TracesEndpoint;

        public string TracesEndpoint { get; }

        public string? LogsEndpoint { get; }

        public string? TracesAuthHeader { get; }

        public string? LogsAuthHeader { get; }

        public ForwarderConfig(ILogger<ForwarderConfig> log)
            : this(
                Environment.GetEnvironmentVariable("OTLP_TRACES_ENDPOINT"),
                Environment.GetEnvironmentVariable("OTLP_LOGS_ENDPOINT"),
                Environment.GetEnvironmentVariable("OTLP_ENDPOINT"),
                Environment.GetEnvironmentVariable("OTLP_TRACES_HEADER_AUTHORIZATION"),
                Environment.GetEnvironmentVariable("OTLP_LOGS_HEADER_AUTHORIZATION"),
                Environment.GetEnvironmentVariable("OTLP_HEADER_AUTHORIZATION"),
                log)
        {
        }

        public ForwarderConfig(
            string? tracesEndpoint,
            string? logsEndpoint,
            string? baseEndpoint,
            string? tracesAuthHeader = null,
            string? logsAuthHeader = null,
            string? baseAuthHeader = null,
            ILogger<ForwarderConfig>? log = null)
        {
            // A baseEndpoint that already carries a signal-specific path (/v1/traces or /v1/logs)
            // is treated as fallback for that signal only, preserving back-compat for deployments
            // that historically set OTLP_ENDPOINT to a full traces URL.
            var baseHasTracesPath = !string.IsNullOrEmpty(baseEndpoint) && baseEndpoint!.Contains(TracesPath);
            var baseHasLogsPath = !string.IsNullOrEmpty(baseEndpoint) && baseEndpoint!.Contains(LogsPath);
            var baseIsBareUrl = !string.IsNullOrEmpty(baseEndpoint) && !baseHasTracesPath && !baseHasLogsPath;

            var tracesSource = !string.IsNullOrEmpty(tracesEndpoint)
                ? tracesEndpoint
                : (baseHasLogsPath ? null : baseEndpoint);

            if (!string.IsNullOrEmpty(tracesSource))
            {
                TracesEndpoint = EnsureSuffix(tracesSource!, TracesPath);
                log?.LogDebug("Using OTLP traces endpoint: " + TracesEndpoint);
            }
            else
            {
                TracesEndpoint = DefaultBase + TracesPath;
                log?.LogWarning("No OTLP traces endpoint configured. Defaulting to: " + TracesEndpoint);
            }

            var logsSource = !string.IsNullOrEmpty(logsEndpoint)
                ? logsEndpoint
                : ((baseIsBareUrl || baseHasLogsPath) ? baseEndpoint : null);

            if (!string.IsNullOrEmpty(logsSource))
            {
                LogsEndpoint = EnsureSuffix(logsSource!, LogsPath);
                log?.LogDebug("Using OTLP logs endpoint: " + LogsEndpoint);
            }
            else
            {
                LogsEndpoint = null;
                log?.LogInformation("OTLP logs endpoint not configured. AppExceptions and AppTraces records will be skipped.");
            }

            // Authorization headers: explicit per-signal wins, else fall back to the shared base header.
            TracesAuthHeader = !string.IsNullOrEmpty(tracesAuthHeader)
                ? tracesAuthHeader
                : (!string.IsNullOrEmpty(baseAuthHeader) ? baseAuthHeader : null);

            LogsAuthHeader = !string.IsNullOrEmpty(logsAuthHeader)
                ? logsAuthHeader
                : (!string.IsNullOrEmpty(baseAuthHeader) ? baseAuthHeader : null);
        }

        private static string EnsureSuffix(string url, string suffix)
        {
            if (url.Contains(suffix))
                return url;
            return url.EndsWith("/") ? url + suffix.TrimStart('/') : url + suffix;
        }
    }
}
