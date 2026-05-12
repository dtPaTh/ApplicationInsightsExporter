using ApplicationInsightsForwarderWorker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddSingleton<ForwarderConfig>();

builder.Services.AddHttpClient("ApplicationInsightsExporter", config => {
    config.DefaultRequestHeaders.UserAgent.ParseAdd(ForwarderInfo.UserAgent);

    var authHeader = Environment.GetEnvironmentVariable("OTLP_HEADER_AUTHORIZATION");
    if (!String.IsNullOrEmpty(authHeader))
        config.DefaultRequestHeaders.Add("Authorization", authHeader);

});

builder.Services.AddSingleton<ApplicationInsights2OTLP.Convert>(sp =>
    new ApplicationInsights2OTLP.Convert(
        sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>(),
        ForwarderInfo.UserAgent));

await builder.Build().RunAsync();