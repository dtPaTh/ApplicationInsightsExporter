using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationInsightsForwarderWorker
{
    public class ForwarderConfig
    {
        private readonly string _OTLPEndpoint; 
        public string OTLPEndpoint 
        { 
            get 
            { 
                return _OTLPEndpoint; 
            }
        }

        public ForwarderConfig(ILogger<ForwarderConfig> log)
            : this(Environment.GetEnvironmentVariable("OTLP_ENDPOINT"), log)
        {
        }

        public ForwarderConfig(string? otlpEndpoint, ILogger<ForwarderConfig>? log = null)
        {
            if (!String.IsNullOrEmpty(otlpEndpoint))
            {
                _OTLPEndpoint = otlpEndpoint;
                if (!_OTLPEndpoint.Contains("v1/traces"))
                {
                    if (_OTLPEndpoint.EndsWith("/"))
                        _OTLPEndpoint += "v1/traces";
                    else
                        _OTLPEndpoint += "/v1/traces";
                }
                log?.LogDebug("Using OTLP Endpoint: " + _OTLPEndpoint);
            }
            else
            {
                _OTLPEndpoint = "http://localhost:4318/v1/traces";
                log?.LogWarning("OTLP_ENDPOINT environment variable is not set. Defaulting to: " + _OTLPEndpoint);
            }
        }


    }
}
