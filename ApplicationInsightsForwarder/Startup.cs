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
                var productValue = new ProductInfoHeaderValue("ApplicationInsightsForwarder", "1.0");
                var commentValue = new ProductInfoHeaderValue("(+https://github.com/dtPaTh/ApplicationInsightsExporter)");

                config.DefaultRequestHeaders.UserAgent.Add(productValue);
                config.DefaultRequestHeaders.UserAgent.Add(commentValue);

                var authHeader = Environment.GetEnvironmentVariable("OTLP_HEADER_AUTHORIZATION");
                if (!String.IsNullOrEmpty(authHeader))
                    config.DefaultRequestHeaders.Add("Authorization", authHeader);

                

            });

            builder.Services.AddSingleton<ApplicationInsights2OTLP.Convert>();


        }
    }


}