using System;
using Google.Protobuf;
using Opentelemetry.Proto.Trace.V1;
using Opentelemetry.Proto.Common.V1;
using Opentelemetry.Proto.Resource.V1;
using System.Text.Json;
using System.Collections.Generic;
using Opentelemetry.Proto.Collector.Trace.V1;
using System.Text;
using Newtonsoft.Json.Linq;
using OpenTelemetry;
using ApplicationInsights;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using AISemConv = ApplicationInsights.SemanticConventions;
using OTelSemConv = OpenTelemetry.SemanticConventions;
using System.ComponentModel.Design;
using System.Text.RegularExpressions;

namespace ApplicationInsights2OTLP
{
    //
    //AppInsights Telemetry mapping: https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/exporter/azuremonitorexporter
    //
    public class Convert
    {
        private readonly ILogger _logger;

        public readonly bool _SimulateRealtime = false;

        private static readonly Regex TraceIdRegex = new Regex("^[a-fA-F0-9]{32}$", RegexOptions.Compiled);


        public Convert(ILoggerFactory? loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger<Convert>()
              ?? NullLoggerFactory.Instance.CreateLogger<Convert>();
        }
#if DEBUG
        public Convert(ILoggerFactory? loggerFactory,bool simulateRealtime): this(loggerFactory)
        {
            _SimulateRealtime = simulateRealtime;
        }
#endif
        private Span.Types.SpanKind MapSpanKind(string telemetryType, string dependencyType)
        {
            if (telemetryType == TelemetryTypes.AppRequests)
                return Span.Types.SpanKind.Server;
            else if (telemetryType == TelemetryTypes.AppDependencies)
            {
                switch (dependencyType)
                {
                    case DependencyTypes.AzureServiceBusMessage:
                        return Span.Types.SpanKind.Producer;
                    default:
                        return Span.Types.SpanKind.Client;
                }
            }

            return Span.Types.SpanKind.Server;
        }

        private string NormalizeKeyName(string key)
        {
            return key.ToLower().Replace(" ", "_");
        }
        private KeyValuePair<string,string>? MapProperties(string key, string value)
        {
            string? newKey = null;
            string newVal = String.Empty;

            switch (key)
            {
                //skip as already set for resource attributes
                case Properties.HostInstanceId: 
                case Properties.ProcessId:
                //skip as known to be useless
                case Properties.OriginalFormat:
                case Properties.LogLevel:
                case Properties.Category:
                    return null;
                default:
                    newKey = AISemConv.ScopeAppInsights+"."+ AISemConv.ScopeProperties+"."+NormalizeKeyName(key); break;
            }

            if (newKey != null && String.IsNullOrEmpty(newVal))
                newVal = value;

            if (newKey != null)
                return new KeyValuePair<string, string>(newKey, newVal);
            else
                return null;
        }

        public DateTimeOffset ParseTimestamp(string ts)
        {
#if DEBUG
            if (_SimulateRealtime)
                return DateTimeOffset.UtcNow;
            else
#endif
                return DateTimeOffset.Parse(ts);
        }

        public static bool IsValidTraceId(string traceId)
        {
            if (string.IsNullOrEmpty(traceId))
            {
                return false;
            }

            return TraceIdRegex.IsMatch(traceId);
        }

        internal string ParseTraceId(string traceid)
        {

#if DEBUG
            if (_SimulateRealtime) //generate a unique trace-id per run 
                traceid = BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", string.Empty).ToLower();
#endif
            if (!IsValidTraceId(traceid))
                traceid = string.Empty;
                
            return traceid;
            
        }

        public ulong ConvertTimeStampToNano(string ts)
        {
            var d = ParseTimestamp(ts);

            DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (ulong)(d - epochStart).Ticks * 100;
        }

        public ulong ConvertTimeSpanToNano(string ts, double durationMS)
        {
            var d = ParseTimestamp(ts);
            d = d.AddMilliseconds(durationMS);
            DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (ulong)(d - epochStart).Ticks * 100;
            
        }

        public bool TryAddResourceAttribute(ResourceSpans s, string key, string value)
        {
            if (!String.IsNullOrEmpty(value))
            {
                s.Resource.Attributes.Add(new KeyValue()
                {
                    Key = key,
                    Value = new AnyValue()
                    {
                        StringValue = value
                    }
                });

                return true;
            }
            return false;
        }

        public bool TryAddAttribute(Span s, string key, string value)
        {
            if (!String.IsNullOrEmpty(value))
            {
                s.Attributes.Add(new KeyValue()
                {
                    Key = key,
                    Value = new AnyValue()
                    {
                        StringValue = value
                    }
                }); ;
                return true;
            }
            return false;

        }

        public bool TryMapProperties(Span s, string key, string value)
        {
            var mapped = MapProperties(key, value);
            if (mapped.HasValue)
            {
                s.Attributes.Add(new KeyValue()
                {
                    Key = mapped.Value.Key,
                    Value = new AnyValue()
                    {
                        StringValue = mapped.Value.Value
                    }
                });

                return true;
            }

            return false;
        }

        internal ByteString ConvertToByteString(string str)
        {
            byte[] byteArray = new byte[str.Length / 2];
            for (int i = 0; i < byteArray.Length; i++)
            {
                byteArray[i] = System.Convert.ToByte(str.Substring(i * 2, 2), 16);
            }

            ByteString byteStr = ByteString.CopyFrom(byteArray);

            return byteStr;
        }

        internal string Value(JsonElement e, string key)
        {
            JsonElement val;
            if (e.TryGetProperty(key, out val))
            {
                var r = val.GetString();
                if (!String.IsNullOrEmpty(r)) 
                    return r;
            }
            else
            {
                _logger.LogDebug("Missing property '" + key + "'");
            }

            return "";

        }

        public ExportTraceServiceRequest FromApplicationInsights(string appInsightsJsonStr)
        {

            _logger.LogDebug(appInsightsJsonStr);

            var export = new ExportTraceServiceRequest();

            var resSpan = new ResourceSpans();
            export.ResourceSpans.Add(resSpan);

            var root = JsonDocument.Parse(appInsightsJsonStr);

            var t = root.RootElement.GetProperty(TelemetryConstants.Records).EnumerateArray();
            while (t.MoveNext())
            {
                var operationId = Value(t.Current, Attributes.OperationId);
                var traceId = ParseTraceId(operationId);
                if (String.IsNullOrEmpty(traceId))
                {
                    _logger.LogWarning("Skip processing telemetry! Property '" + Attributes.OperationId + "' is invalid ('" + operationId + "'). Please make sure W3C TraceContext standard is is configured.");
                    continue;
                }

                var parentId = Value(t.Current, Attributes.ParentId); 
                if (parentId == Value(t.Current, Attributes.OperationId))
                    parentId = "";

                JsonElement properties;
                bool hasProperties = false;

                resSpan.Resource = new Resource();
                
                TryAddResourceAttribute(resSpan, OTelSemConv.AttributeServiceName,Value(t.Current, Attributes.AppRoleName));
                TryAddResourceAttribute(resSpan, OTelSemConv.AttributeServiceInstance, Value(t.Current, Attributes.AppRoleInstance));
                if (t.Current.TryGetProperty(TelemetryConstants.Properties, out properties))
                {
                    hasProperties = true;
                    TryAddResourceAttribute(resSpan, OTelSemConv.AttributeProcessId, Value(properties, Properties.ProcessId));
                    TryAddResourceAttribute(resSpan, OTelSemConv.AttributeHostId, Value(properties, Properties.HostInstanceId));
                }

                var libSpan = new InstrumentationLibrarySpans();

                var instr = Value(t.Current, Attributes.SDKVersion).Split(new char[] {':'});

                libSpan.InstrumentationLibrary = new InstrumentationLibrary()
                {
                    Name = instr[0] ?? ApplicationInsights.SemanticConventions.InstrumentationLibraryName,
                    Version = instr[1] ?? String.Empty
                };
                resSpan.InstrumentationLibrarySpans.Add(libSpan);

                var span = new Span();
                libSpan.Spans.Add(span);

                span.TraceId = ConvertToByteString(traceId);
                span.SpanId = ConvertToByteString(Value(t.Current, Attributes.Id));
                if (!String.IsNullOrEmpty(parentId))
                    span.ParentSpanId = ConvertToByteString(parentId);

                var spanType = Value(t.Current, Attributes.Type);
                string spanName = Value(t.Current, Attributes.Name);

                TryAddAttribute(span, AISemConv.TelemetryType, spanType);
                if (spanType == TelemetryTypes.AppDependencies)
                {
                    try
                    {
                        string dt = Value(t.Current, Attributes.DependencyType);

                        span.Kind = MapSpanKind(spanType, dt);
                        
                        switch (dt)
                        {
                            case DependencyTypes.Http: 
                            case DependencyTypes.HttpTracked:
                            {
                                var req = Value(t.Current, Attributes.Name).Split(new char[] { ' ' });

                                TryAddAttribute(span, OTelSemConv.AttributeHttpMethod, req[0]);
                                TryAddAttribute(span, OTelSemConv.AttributeHttpTarget, req[1]);
                                TryAddAttribute(span, OTelSemConv.AttributeHttpUrl, Value(t.Current, Attributes.Data));
                                TryAddAttribute(span, OTelSemConv.AttributeHttpStatusCode, Value(t.Current, Attributes.ResultCode));
                                break;
                            }
                            case DependencyTypes.Backend:
                            {
                                spanName = DependencyTypes.Backend.ToUpper() + " " + spanName;

                                var req = Value(t.Current, Attributes.Data).Split(" - ");

                                TryAddAttribute(span, OTelSemConv.AttributeHttpUrl, req[1]);
                                TryAddAttribute(span, OTelSemConv.AttributeHttpMethod, req[0]);
                                TryAddAttribute(span, OTelSemConv.AttributeHttpTarget, Value(t.Current, Attributes.Target));
                                TryAddAttribute(span, OTelSemConv.AttributeHttpStatusCode, Value(t.Current, Attributes.ResultCode));
                                break;
                            }
                            case DependencyTypes.AzureServiceBus:
                            case DependencyTypes.AzureServiceBusMessage:
                            {
                                var target = Value(t.Current, Attributes.Target).Split(new char[] { '/' });
                                TryAddAttribute(span, OTelSemConv.AttributePeerService,  target[0]);
                                TryAddAttribute(span, OTelSemConv.AttributeMessageBusDestination, target[1]);

                                TryAddAttribute(span, OTelSemConv.AttributeMessagingSystem, OTelSemConv.MessagingSystemAzureServiceBus);
                                TryAddAttribute(span, OTelSemConv.AttributeMessagingDestination, Value(t.Current, Attributes.Target));
                                
                                if (dt == DependencyTypes.AzureServiceBus)
                                    TryAddAttribute(span, OTelSemConv.AttributeMessagingOperation, OTelSemConv.MessagingOperationDeliver);
                                else
                                    TryAddAttribute(span, OTelSemConv.AttributeMessagingOperation, OTelSemConv.MessagingOperationCreate);
                                
                                break;
                            }
                        }

                        TryAddAttribute(span, AISemConv.DependencyType, dt);
                        

                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error parsing telemetry of type 'AppDependency'");
                    }
                }
                else
                {
                    span.Kind = MapSpanKind(spanType, String.Empty);
                }
                
                TryAddAttribute(span, AISemConv.OperationName, Value(t.Current, Attributes.OperationName));
                TryAddAttribute(span, AISemConv.Target, Value(t.Current, Attributes.Source));
                TryAddAttribute(span, AISemConv.Source, Value(t.Current, Attributes.Target));
                TryAddAttribute(span, AISemConv.Url, Value(t.Current, Attributes.Url));
                TryAddAttribute(span, AISemConv.Data, Value(t.Current, Attributes.Data));
                TryAddAttribute(span, AISemConv.Status, Value(t.Current, Attributes.Status));
                TryAddAttribute(span, AISemConv.ResultCode, Value(t.Current, Attributes.ResultCode));


                span.Name = spanName;
                span.StartTimeUnixNano = ConvertTimeStampToNano(Value(t.Current, Attributes.Time));
                span.EndTimeUnixNano = ConvertTimeSpanToNano(Value(t.Current, Attributes.Time), t.Current.GetProperty(Attributes.Duration).GetDouble());

                if (hasProperties)
                {
                    var l = properties.EnumerateObject();
                    while (l.MoveNext())
                    {
                        TryMapProperties(span, l.Current.Name, l.Current.Value.GetString());
                    }
                }
                
            }

            return export;
        }
    }
}