using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http.Headers;

[assembly: FunctionsStartup(typeof(ApplicationInsightsForwarder.Startup))]

namespace ApplicationInsightsForwarder
{

    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient("ApplicationInsightsExporter", config =>
            {
                var productValue = new ProductInfoHeaderValue("ApplicationInsightsExporter", "1.0");
                var commentValue = new ProductInfoHeaderValue("(+https://github.com/dtPaTh/ApplicationInsightsExporter)");

                config.DefaultRequestHeaders.UserAgent.Add(productValue);
                config.DefaultRequestHeaders.UserAgent.Add(commentValue);
            });

            builder.Services.AddSingleton<ApplicationInsights2OTLP.Convert>();


        }
    }


}