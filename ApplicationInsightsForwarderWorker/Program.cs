using ApplicationInsightsForwarderWorker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Headers;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddSingleton<ForwarderConfig>();

builder.Services.AddHttpClient("ApplicationInsightsExporter", config => {
    var productValue = new ProductInfoHeaderValue("ApplicationInsightsForwarder", "1.1");
    var commentValue = new ProductInfoHeaderValue("(+https://github.com/dtPaTh/ApplicationInsightsExporter)");

    config.DefaultRequestHeaders.UserAgent.Add(productValue);
    config.DefaultRequestHeaders.UserAgent.Add(commentValue);

    var authHeader = Environment.GetEnvironmentVariable("OTLP_HEADER_AUTHORIZATION");
    if (!String.IsNullOrEmpty(authHeader))
        config.DefaultRequestHeaders.Add("Authorization", authHeader);

});

builder.Services.AddSingleton<ApplicationInsights2OTLP.Convert>();

await builder.Build().RunAsync();