using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace ApplicationInsights
{
    internal class DependencyTypes
    {
        public const string Http = "HTTP";
        public const string HttpTracked = "Http (tracked component)"; //https://github.com/microsoft/ApplicationInsights-dotnet-server/issues/323 tracked by another AI instance
        public const string Backend = "Backend"; //APIM
        public const string AzureServiceBus = "Azure Service Bus";
        public const string AzureServiceBusMessage = "Queue Message | Azure Service Bus";


    }
}
