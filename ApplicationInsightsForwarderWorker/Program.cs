using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Headers;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        //services.AddApplicationInsightsTelemetryWorkerService();
        //services.ConfigureFunctionsApplicationInsights();

        services.AddHttpClient("ApplicationInsightsExporter", config =>
        {
            var productValue = new ProductInfoHeaderValue("ApplicationInsightsForwarder", "1.0");
            var commentValue = new ProductInfoHeaderValue("(+https://github.com/dtPaTh/ApplicationInsightsExporter)");

            config.DefaultRequestHeaders.UserAgent.Add(productValue);
            config.DefaultRequestHeaders.UserAgent.Add(commentValue);

            var authHeader = Environment.GetEnvironmentVariable("OTLP_HEADER_AUTHORIZATION");
            if (!String.IsNullOrEmpty(authHeader))
                config.DefaultRequestHeaders.Add("Authorization", authHeader);

        });

        services.AddSingleton<ApplicationInsights2OTLP.Convert>();
    })
    .Build();

host.Run();
