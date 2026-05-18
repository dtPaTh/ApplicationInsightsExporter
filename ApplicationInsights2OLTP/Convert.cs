using System;
using Google.Protobuf;
using OpenTelemetry.Proto.Trace.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using System.Text.Json;
using System.Collections.Generic;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Collector.Logs.V1;
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

        private readonly string? _producerUserAgent;

        private static readonly Regex TraceIdRegex = new Regex("^[a-fA-F0-9]{32}$", RegexOptions.Compiled);


        public Convert(ILoggerFactory? loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger<Convert>()
              ?? NullLoggerFactory.Instance.CreateLogger<Convert>();
        }

        public Convert(ILoggerFactory? loggerFactory, string? producerUserAgent) : this(loggerFactory)
        {
            _producerUserAgent = producerUserAgent;
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
            return TryAddResourceAttribute(s.Resource, key, value);
        }

        private bool TryAddResourceAttribute(Resource r, string key, string value)
        {
            if (!String.IsNullOrEmpty(value))
            {
                r.Attributes.Add(new KeyValue()
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

        private Resource BuildResource(JsonElement record)
        {
            var resource = new Resource();
            TryAddResourceAttribute(resource, OTelSemConv.AttributeServiceName, Value(record, Attributes.AppRoleName));
            TryAddResourceAttribute(resource, OTelSemConv.AttributeServiceInstance, Value(record, Attributes.AppRoleInstance));
            TryAddResourceAttribute(resource, AISemConv.ForwarderUserAgent, _producerUserAgent ?? "");
            if (record.TryGetProperty(TelemetryConstants.Properties, out var props))
            {
                TryAddResourceAttribute(resource, OTelSemConv.AttributeProcessId, Value(props, Properties.ProcessId));
                TryAddResourceAttribute(resource, OTelSemConv.AttributeHostId, Value(props, Properties.HostInstanceId));
            }
            return resource;
        }

        private InstrumentationScope BuildScope(JsonElement record)
        {
            var sdkVersion = Value(record, Attributes.SDKVersion);
            var parts = sdkVersion.Split(new[] { ':' }, 2);
            return new InstrumentationScope()
            {
                Name = !string.IsNullOrEmpty(parts[0]) ? parts[0] : AISemConv.InstrumentationScopeName,
                Version = parts.Length > 1 ? parts[1].Trim() : string.Empty
            };
        }

        private static (SeverityNumber Number, string Text) MapSeverity(int aiSeverityLevel)
        {
            return aiSeverityLevel switch
            {
                0 => (SeverityNumber.Debug, "DEBUG"),
                1 => (SeverityNumber.Info, "INFO"),
                2 => (SeverityNumber.Warn, "WARN"),
                3 => (SeverityNumber.Error, "ERROR"),
                4 => (SeverityNumber.Fatal, "FATAL"),
                _ => (SeverityNumber.Unspecified, "")
            };
        }

        private string BuildStackTrace(JsonElement record)
        {
            if (!record.TryGetProperty(Attributes.Details, out var details) || details.ValueKind != JsonValueKind.Array)
                return string.Empty;

            var sb = new StringBuilder();
            bool firstException = true;
            foreach (var ex in details.EnumerateArray())
            {
                if (!firstException)
                    sb.AppendLine().Append(" ---> ");

                var typeName = Value(ex, "typeName");
                var message = Value(ex, "message");
                if (!string.IsNullOrEmpty(typeName) || !string.IsNullOrEmpty(message))
                    sb.Append(typeName).Append(": ").Append(message);

                if (ex.TryGetProperty(Attributes.ParsedStack, out var frames) && frames.ValueKind == JsonValueKind.Array)
                {
                    foreach (var frame in frames.EnumerateArray())
                    {
                        sb.AppendLine().Append("   at ");
                        var assembly = Value(frame, Attributes.StackAssembly);
                        var method = Value(frame, Attributes.StackMethod);
                        var fileName = Value(frame, Attributes.StackFileName);
                        var line = "";
                        if (frame.TryGetProperty(Attributes.StackLine, out var lineEl) && lineEl.ValueKind == JsonValueKind.Number)
                            line = lineEl.GetRawText();

                        if (!string.IsNullOrEmpty(assembly))
                            sb.Append(assembly).Append('!');
                        sb.Append(method);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            sb.Append(" in ").Append(fileName);
                            if (!string.IsNullOrEmpty(line) && line != "0")
                                sb.Append(':').Append(line);
                        }
                    }
                }
                firstException = false;
            }
            return sb.ToString();
        }

        private void TryAddLogAttribute(LogRecord r, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                r.Attributes.Add(new KeyValue()
                {
                    Key = key,
                    Value = new AnyValue() { StringValue = value }
                });
            }
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

        public (ExportTraceServiceRequest Traces, ExportLogsServiceRequest Logs) FromApplicationInsights(string appInsightsJsonStr)
        {
            var root = JsonDocument.Parse(appInsightsJsonStr);

            if (!root.RootElement.TryGetProperty(TelemetryConstants.Records, out var records))
                throw new ArgumentException("No '" + TelemetryConstants.Records + "' found in the provided ApplicationInsights JSON.");

            var traceExport = new ExportTraceServiceRequest();
            var resSpan = new ResourceSpans();
            traceExport.ResourceSpans.Add(resSpan);

            var logsExport = new ExportLogsServiceRequest();
            var resLogs = new ResourceLogs();
            logsExport.ResourceLogs.Add(resLogs);

            var t = records.EnumerateArray();
            while (t.MoveNext())
            {
                var recordType = Value(t.Current, Attributes.Type);
                switch (recordType)
                {
                    case TelemetryTypes.AppRequests:
                    case TelemetryTypes.AppDependencies:
                        AppendSpan(t.Current, recordType, resSpan);
                        break;
                    case TelemetryTypes.AppExceptions:
                    case TelemetryTypes.AppTraces:
                        AppendLog(t.Current, recordType, resLogs);
                        break;
                    default:
                        _logger.LogDebug("Skipping unsupported telemetry type '" + recordType + "'");
                        break;
                }
            }

            return (traceExport, logsExport);
        }

        private void AppendSpan(JsonElement record, string spanType, ResourceSpans resSpan)
        {
            var operationId = Value(record, Attributes.OperationId);
            var traceId = ParseTraceId(operationId);
            if (String.IsNullOrEmpty(traceId))
            {
                _logger.LogWarning("Skip processing telemetry! Property '" + Attributes.OperationId + "' is invalid ('" + operationId + "'). Please make sure W3C TraceContext standard is is configured.");
                return;
            }

            var parentId = Value(record, Attributes.ParentId);
            if (parentId == operationId)
                parentId = "";

            resSpan.Resource = BuildResource(record);
            bool hasProperties = record.TryGetProperty(TelemetryConstants.Properties, out var properties);

            var scopeSpan = new ScopeSpans();
            scopeSpan.Scope = BuildScope(record);
            resSpan.ScopeSpans.Add(scopeSpan);

            var span = new Span();
            scopeSpan.Spans.Add(span);

            span.TraceId = ConvertToByteString(traceId);
            span.SpanId = ConvertToByteString(Value(record, Attributes.Id));
            if (!String.IsNullOrEmpty(parentId))
                span.ParentSpanId = ConvertToByteString(parentId);

            string spanName = Value(record, Attributes.Name);

            TryAddAttribute(span, AISemConv.TelemetryType, spanType);
            if (spanType == TelemetryTypes.AppDependencies)
            {
                try
                {
                    string dt = Value(record, Attributes.DependencyType);

                    span.Kind = MapSpanKind(spanType, dt);

                    switch (dt)
                    {
                        case DependencyTypes.Http:
                        case DependencyTypes.HttpTracked:
                            {
                                var req = Value(record, Attributes.Name).Split(new char[] { ' ' });

                                TryAddAttribute(span, OTelSemConv.AttributeHttpMethod, req[0]);
                                TryAddAttribute(span, OTelSemConv.AttributeHttpTarget, req[1]);
                                TryAddAttribute(span, OTelSemConv.AttributeHttpUrl, Value(record, Attributes.Data));
                                TryAddAttribute(span, OTelSemConv.AttributeHttpStatusCode, Value(record, Attributes.ResultCode));
                                break;
                            }
                        case DependencyTypes.Backend:
                            {
                                spanName = DependencyTypes.Backend.ToUpper() + " " + spanName;

                                var req = Value(record, Attributes.Data).Split(" - ");

                                TryAddAttribute(span, OTelSemConv.AttributeHttpUrl, req[1]);
                                TryAddAttribute(span, OTelSemConv.AttributeHttpMethod, req[0]);
                                TryAddAttribute(span, OTelSemConv.AttributeHttpTarget, Value(record, Attributes.Target));
                                TryAddAttribute(span, OTelSemConv.AttributeHttpStatusCode, Value(record, Attributes.ResultCode));
                                break;
                            }
                        case DependencyTypes.AzureServiceBus:
                        case DependencyTypes.AzureServiceBusMessage:
                            {
                                var target = Value(record, Attributes.Target).Split(new char[] { '/' });
                                TryAddAttribute(span, OTelSemConv.AttributePeerService, target[0]);
                                TryAddAttribute(span, OTelSemConv.AttributeMessageBusDestination, target[1]);

                                TryAddAttribute(span, OTelSemConv.AttributeMessagingSystem, OTelSemConv.MessagingSystemAzureServiceBus);
                                TryAddAttribute(span, OTelSemConv.AttributeMessagingDestination, Value(record, Attributes.Target));

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

            TryAddAttribute(span, AISemConv.OperationName, Value(record, Attributes.OperationName));
            TryAddAttribute(span, AISemConv.Target, Value(record, Attributes.Target));
            TryAddAttribute(span, AISemConv.Source, Value(record, Attributes.Source));
            TryAddAttribute(span, AISemConv.Url, Value(record, Attributes.Url));
            TryAddAttribute(span, AISemConv.Data, Value(record, Attributes.Data));
            TryAddAttribute(span, AISemConv.Status, Value(record, Attributes.Status));
            TryAddAttribute(span, AISemConv.ResultCode, Value(record, Attributes.ResultCode));

            if (record.TryGetProperty(Attributes.Success, out var successProp))
            {
                bool explicitlyFailed = successProp.ValueKind == JsonValueKind.False
                    || (successProp.ValueKind == JsonValueKind.String
                        && string.Equals(successProp.GetString(), "false", StringComparison.OrdinalIgnoreCase));
                if (explicitlyFailed)
                    span.Status = new Status { Code = Status.Types.StatusCode.Error };
            }

            span.Name = spanName;
            span.StartTimeUnixNano = ConvertTimeStampToNano(Value(record, Attributes.Time));
            span.EndTimeUnixNano = ConvertTimeSpanToNano(Value(record, Attributes.Time), record.GetProperty(Attributes.Duration).GetDouble());

            if (hasProperties)
            {
                var l = properties.EnumerateObject();
                while (l.MoveNext())
                {
                    TryMapProperties(span, l.Current.Name, l.Current.Value.GetString());
                }
            }
        }

        private void AppendLog(JsonElement record, string recordType, ResourceLogs resLogs)
        {
            var operationId = Value(record, Attributes.OperationId);
            var traceId = ParseTraceId(operationId);
            if (string.IsNullOrEmpty(traceId))
            {
                _logger.LogWarning("Skip processing telemetry! Property '" + Attributes.OperationId + "' is invalid ('" + operationId + "'). Please make sure W3C TraceContext standard is configured.");
                return;
            }

            resLogs.Resource = BuildResource(record);
            bool hasProperties = record.TryGetProperty(TelemetryConstants.Properties, out var properties);

            var scopeLogs = new ScopeLogs();
            scopeLogs.Scope = BuildScope(record);
            resLogs.ScopeLogs.Add(scopeLogs);

            var log = new LogRecord();
            scopeLogs.LogRecords.Add(log);

            log.TimeUnixNano = ConvertTimeStampToNano(Value(record, Attributes.Time));
            log.ObservedTimeUnixNano = log.TimeUnixNano;
            log.TraceId = ConvertToByteString(traceId);
            var parentId = Value(record, Attributes.ParentId);
            if (!string.IsNullOrEmpty(parentId) && parentId != operationId)
                log.SpanId = ConvertToByteString(parentId);

            TryAddLogAttribute(log, AISemConv.TelemetryType, recordType);

            if (recordType == TelemetryTypes.AppExceptions)
            {
                MapException(record, log);
            }
            else // AppTraces
            {
                MapTrace(record, log);
            }

            if (hasProperties)
            {
                foreach (var prop in properties.EnumerateObject())
                {
                    var mapped = MapProperties(prop.Name, prop.Value.GetString() ?? string.Empty);
                    if (mapped.HasValue)
                        TryAddLogAttribute(log, mapped.Value.Key, mapped.Value.Value);
                }
            }
        }

        private void MapException(JsonElement record, LogRecord log)
        {
            var exceptionType = Value(record, Attributes.ExceptionType);
            var message = Value(record, Attributes.Message);
            if (string.IsNullOrEmpty(message))
                message = Value(record, Attributes.OuterMessage);

            int aiSeverity = 3; // default: ERROR
            if (record.TryGetProperty(Attributes.SeverityLevel, out var sev) && sev.ValueKind == JsonValueKind.Number)
                aiSeverity = sev.GetInt32();

            var (sevNum, sevText) = MapSeverity(aiSeverity);
            log.SeverityNumber = sevNum;
            log.SeverityText = sevText;

            log.Body = new AnyValue() { StringValue = message };

            TryAddLogAttribute(log, OTelSemConv.AttributeExceptionType, exceptionType);
            TryAddLogAttribute(log, OTelSemConv.AttributeExceptionMessage, message);
            TryAddLogAttribute(log, OTelSemConv.AttributeExceptionStacktrace, BuildStackTrace(record));
            TryAddLogAttribute(log, AISemConv.ExceptionProblemId, Value(record, Attributes.ProblemId));
            TryAddLogAttribute(log, AISemConv.ExceptionHandledAt, Value(record, Attributes.HandledAt));
        }

        private void MapTrace(JsonElement record, LogRecord log)
        {
            var message = Value(record, Attributes.Message);
            int aiSeverity = 1; // default: INFO
            if (record.TryGetProperty(Attributes.SeverityLevel, out var sev) && sev.ValueKind == JsonValueKind.Number)
                aiSeverity = sev.GetInt32();

            var (sevNum, sevText) = MapSeverity(aiSeverity);
            log.SeverityNumber = sevNum;
            log.SeverityText = sevText;
            log.Body = new AnyValue() { StringValue = message };
        }
    }
}
