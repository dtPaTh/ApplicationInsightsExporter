using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace ApplicationInsightsForwarder
{
    public class ApplicationInsightsForwarder
    {
        HttpClient _client;
        ApplicationInsights2OTLP.Convert _converter;
        public ApplicationInsightsForwarder(IHttpClientFactory httpClientFactory, ApplicationInsights2OTLP.Convert otlpConverter)
        {
            _client = httpClientFactory.CreateClient();
            _converter = otlpConverter;
        }

        [FunctionName("ForwardAI")]
        public async Task Run([EventHubTrigger("appinsights", Connection = "EHConnection")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);

                    var exportTraceServiceRequest = _converter.FromApplicationInsights(messageBody);
                    if (exportTraceServiceRequest == null) // if format was not able to be processed/mapped.. 
                        continue;

                    
                    _client.DefaultRequestHeaders.Clear();

                    var authHeader = Environment.GetEnvironmentVariable("OTLP_HEADER_AUTHORIZATION");
                    if (!String.IsNullOrEmpty(authHeader))
                        _client.DefaultRequestHeaders.Add("Authorization", authHeader); 

                    var otlpEndpoint = Environment.GetEnvironmentVariable("OTLP_ENDPOINT");
                    if (!otlpEndpoint.Contains("v1/traces"))
                        if (otlpEndpoint.EndsWith("/"))
                            otlpEndpoint = otlpEndpoint+="v1/traces";
                        else
                            otlpEndpoint = otlpEndpoint += "/v1/traces";

                    var content = new ApplicationInsights2OTLP.ExportRequestContent(exportTraceServiceRequest);

                    var res = await _client.PostAsync(otlpEndpoint, content);
                    if (!res.IsSuccessStatusCode)
                    {
                        log.LogError("Couldn't send span " + (res.StatusCode) + "\n" + messageBody );
                    }

                    await Task.Yield();
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}
