using System;
using System.Collections.Generic;
using System.Text;

namespace ApplicationInsights
{
    //Operation types: https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/monitor/Azure.Monitor.OpenTelemetry.Exporter/src/Internals/OperationType.cs
    //Unknown,
    //Azure,
    //Common,
    //Db,
    //FaaS,
    //Http,
    //Messaging,
    //Rpc
    internal class OperationTypes
    {
        public const string Messaging = "messaging";
    }
}
