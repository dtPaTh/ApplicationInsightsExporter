using System;
using System.Text;
using Azure.Messaging.EventHubs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ApplicationInsightsForwarderWorker
{
    public class ApplicationInsightsForwarder
    {
        private readonly ILogger<ApplicationInsightsForwarder> _logger;
        private readonly ForwarderConfig _config;
        private readonly ApplicationInsights2OTLP.Convert _converter;
        private readonly HttpClient _httpClient;

        public ApplicationInsightsForwarder(ILogger<ApplicationInsightsForwarder> logger, IHttpClientFactory httpClientFactory, ForwarderConfig config, ApplicationInsights2OTLP.Convert otlpConverter)
        {
            _logger = logger;
            _converter = otlpConverter;
            _config = config;

            _httpClient = httpClientFactory.CreateClient("ApplicationInsightsExporter");
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

                    var exportTraceServiceRequest = _converter.FromApplicationInsights(messageBody);
                    if (exportTraceServiceRequest == null) // if format was not able to be processed/mapped.. 
                        continue;

                    var content = new ApplicationInsights2OTLP.ExportRequestContent(exportTraceServiceRequest);

                    var res = await _httpClient.PostAsync(_config.OTLPEndpoint, content);
                    if (!res.IsSuccessStatusCode)
                    {
                        _logger.LogError("Couldn't send spans. HTTP Status:  " + (res.StatusCode));
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
