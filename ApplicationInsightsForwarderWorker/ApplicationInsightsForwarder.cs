using System;
using System.Text;
using Azure.Messaging.EventHubs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ApplicationInsightsForwarderWorker
{
    public class ApplicationInsightsForwarder
    {
        public const string HttpClientName = "ApplicationInsightsExporter";

        private readonly ILogger<ApplicationInsightsForwarder> _logger;
        private readonly ForwarderConfig _config;
        private readonly ApplicationInsights2OTLP.Convert _converter;
        private readonly HttpClient _httpClient;

        public ApplicationInsightsForwarder(ILogger<ApplicationInsightsForwarder> logger, IHttpClientFactory httpClientFactory, ForwarderConfig config, ApplicationInsights2OTLP.Convert otlpConverter)
        {
            _logger = logger;
            _converter = otlpConverter;
            _config = config;

            _httpClient = httpClientFactory.CreateClient(HttpClientName);
        }

        [Function("ForwardAI")]
        public async Task Run([EventHubTrigger("appinsights", Connection = "EHConnection")] EventData[] events)
        {
            foreach (EventData eventData in events)
            {
                string messageBody = string.Empty;
                try
                {
                    byte[] msgBody = eventData.Body.ToArray();
                    messageBody = Encoding.UTF8.GetString(msgBody, 0, msgBody.Length);

                    var (traceRequest, logsRequest) = _converter.FromApplicationInsights(messageBody);

                    if (traceRequest.ResourceSpans.Count > 0
                        && traceRequest.ResourceSpans[0].ScopeSpans.Count > 0
                        && traceRequest.ResourceSpans[0].ScopeSpans[0].Spans.Count > 0)
                    {
                        var content = new ApplicationInsights2OTLP.ExportRequestContent(traceRequest);
                        using var req = new HttpRequestMessage(HttpMethod.Post, _config.TracesEndpoint) { Content = content };
                        if (!string.IsNullOrEmpty(_config.TracesAuthHeader))
                            req.Headers.TryAddWithoutValidation("Authorization", _config.TracesAuthHeader);
                        var res = await _httpClient.SendAsync(req);
                        if (!res.IsSuccessStatusCode)
                            _logger.LogError("Couldn't send spans. HTTP Status: " + res.StatusCode);
                    }

                    if (!string.IsNullOrEmpty(_config.LogsEndpoint)
                        && logsRequest.ResourceLogs.Count > 0
                        && logsRequest.ResourceLogs[0].ScopeLogs.Count > 0
                        && logsRequest.ResourceLogs[0].ScopeLogs[0].LogRecords.Count > 0)
                    {
                        var content = new ApplicationInsights2OTLP.ExportRequestContent(logsRequest);
                        using var req = new HttpRequestMessage(HttpMethod.Post, _config.LogsEndpoint) { Content = content };
                        if (!string.IsNullOrEmpty(_config.LogsAuthHeader))
                            req.Headers.TryAddWithoutValidation("Authorization", _config.LogsAuthHeader);
                        var res = await _httpClient.SendAsync(req);
                        if (!res.IsSuccessStatusCode)
                            _logger.LogError("Couldn't send logs. HTTP Status: " + res.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"An error occurred while processing a message. Error: {ex.Message}. StackTrace: {ex.StackTrace}");
                    _logger.LogDebug("Unprocessed message: ");
                    _logger.LogDebug(messageBody);
                }
            }
        }
    }
}
