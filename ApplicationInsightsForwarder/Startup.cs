using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;

[assembly: FunctionsStartup(typeof(ApplicationInsightsForwarder.Startup))]

namespace ApplicationInsightsForwarder
{

    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient();
            
            builder.Services.AddSingleton<ApplicationInsights2OTLP.Convert>();


        }
    }


}