using NUnit.Framework;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Trace.V1;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ApplicationInsightsTest
{


    public class ApplicationInsights2OTLPTest
    {

        [SetUp]
        public void Setup()
        {
        }

        [TearDown]
        public void Teardown()
        {
        }

        [Test]
        public void ConvertAppRequeste2OTLP()
        {
            string json = File.ReadAllText("samples/AppRequests.json");
            var conv = new ApplicationInsights2OTLP.Convert(null);
            var req = conv.FromApplicationInsights(json).Traces;

            Assert.IsNotNull(req);
            Assert.IsNotNull(req.ResourceSpans);
            Assert.AreEqual(1, req.ResourceSpans.Count, "Expected exactly one ResourceSpans entry");

            var resSpan = req.ResourceSpans[0];
            // resource attributes
            var serviceNameAttr = resSpan.Resource.Attributes.FirstOrDefault(a => a.Key == "service.name");
            Assert.IsNotNull(serviceNameAttr);
            Assert.AreEqual("apprequesttest", serviceNameAttr.Value.StringValue);

            // instrumentation scope
            Assert.IsNotNull(resSpan.ScopeSpans);
            Assert.AreEqual(1, resSpan.ScopeSpans.Count);
            var scope = resSpan.ScopeSpans[0].Scope;
            Assert.IsNotNull(scope);
            Assert.IsTrue(scope.Name.StartsWith("azurefunctions"), "Expected instrumentation scope name to start with 'azurefunctions'");

            // spans
            var spans = resSpan.ScopeSpans[0].Spans;
            Assert.AreEqual(1, spans.Count);
            var span = spans[0];

            // trace and span ids
            Assert.IsNotNull(span.TraceId);
            Assert.AreEqual(16, span.TraceId.Length, "TraceId should be 16 bytes");
            Assert.IsNotNull(span.SpanId);
            Assert.AreEqual(8, span.SpanId.Length, "SpanId should be 8 bytes");

            // kind
            Assert.AreEqual(Span.Types.SpanKind.Server, span.Kind);

            // basic attributes
            var getAttr = new Func<string, string?>(key =>
            {
                var a = span.Attributes.FirstOrDefault(x => x.Key == key);
                return a == null ? null : a.Value.StringValue;
            });

            Assert.AreEqual("AppRequests", getAttr("appinsights.type"));
            Assert.AreEqual("AppRequestTest", span.Name);
            Assert.AreEqual("GET", getAttr("appinsights.prop.httpmethod"));
            Assert.AreEqual("/api/Forward", getAttr("appinsights.prop.httppath"));

            // times
            Assert.Greater(span.StartTimeUnixNano, 0UL);
            Assert.Greater(span.EndTimeUnixNano, span.StartTimeUnixNano);

            // Success=true: status must stay Unset (OTel: Ok requires explicit operator validation, which AI doesn't provide).
            Assert.IsTrue(span.Status == null || span.Status.Code == Status.Types.StatusCode.Unset,
                "Successful AppRequest should leave span Status as Unset, not Ok or Error");
        }

        [Test]
        public void ConvertAppRequestFailed_MapsToErrorStatus()
        {
            string json = File.ReadAllText("samples/AppRequestsFailed.json");
            var conv = new ApplicationInsights2OTLP.Convert(null);
            var req = conv.FromApplicationInsights(json).Traces;

            var span = req.ResourceSpans[0].ScopeSpans[0].Spans[0];
            Assert.AreEqual("FailedRequest", span.Name, "wrong record was mapped");
            Assert.IsNotNull(span.Status, "AppRequest with Success=false should have an explicit Status set");
            Assert.AreEqual(Status.Types.StatusCode.Error, span.Status.Code,
                "AppRequest with Success=false must map to Status.Code=Error");
        }

        [Test]
        public void ConvertAppDependencies2OTLP()
        {
            string json = File.ReadAllText("samples/AppDependencies.json");
            var conv = new ApplicationInsights2OTLP.Convert(null);
            var req = conv.FromApplicationInsights(json).Traces;

            Assert.IsNotNull(req);
            Assert.IsNotNull(req.ResourceSpans);
            Assert.AreEqual(1, req.ResourceSpans.Count);

            var resSpan = req.ResourceSpans[0];
            var spans = resSpan.ScopeSpans[0].Spans;
            Assert.AreEqual(1, spans.Count);
            var span = spans[0];

            // dependency should map to client kind for HTTP
            Assert.AreEqual(Span.Types.SpanKind.Client, span.Kind);

            var getAttr = new Func<string, string?>(key =>
            {
                var a = span.Attributes.FirstOrDefault(x => x.Key == key);
                return a == null ? null : a.Value.StringValue;
            });

            // appinsights type and dependency type
            Assert.AreEqual("AppDependencies", getAttr("appinsights.type"));
            Assert.AreEqual("HTTP", getAttr("appinsights.dependencytype"));

            // target maps from AI 'Target' (regression guard against historical target/source swap)
            Assert.AreEqual("receivecontext.azurewebsites.net", getAttr("appinsights.target"));
            Assert.IsNull(getAttr("appinsights.source"));

            // http semantic attrs
            Assert.AreEqual("GET", getAttr("http.method"));
            Assert.AreEqual("/api/Receiver", getAttr("http.target"));
            Assert.AreEqual("https://someotherservice.urewebsites.net/api/Receiver", getAttr("http.url"));
            Assert.AreEqual("200", getAttr("http.status_code"));

            Assert.Greater(span.StartTimeUnixNano, 0UL);
            Assert.Greater(span.EndTimeUnixNano, span.StartTimeUnixNano);
        }

        [Test]
        public void TraceConversionOnlyEmitsTraceTypes()
        {
            string json = File.ReadAllText("samples/Mixed.json");
            var conv = new ApplicationInsights2OTLP.Convert(null);
            var req = conv.FromApplicationInsights(json).Traces;

            Assert.IsNotNull(req);
            // Mixed.json contains AppRequests + AppExceptions + AppTraces + an unsupported type,
            // with distinct identifiers so we verify the right record produced the span.
            var spans = req.ResourceSpans[0].ScopeSpans[0].Spans;
            Assert.AreEqual(1, spans.Count, "Only the AppRequest record should produce a span; logs and unsupported types must not.");
            Assert.AreEqual("RequestThatShouldSurvive", spans[0].Name);
            var opName = spans[0].Attributes.First(a => a.Key == "appinsights.operationname").Value.StringValue;
            Assert.AreEqual("RequestThatShouldSurvive", opName);
        }

        [Test]
        public void LogConversionEmitsExceptionsAndTracesButNotSpans()
        {
            string json = File.ReadAllText("samples/Mixed.json");
            var conv = new ApplicationInsights2OTLP.Convert(null);
            var req = conv.FromApplicationInsights(json).Logs;

            Assert.IsNotNull(req);
            // Each mapped record gets its own ScopeLogs entry (mirrors the trace-side pattern).
            // Flatten across all ScopeLogs to count total log records.
            var logs = req.ResourceLogs[0].ScopeLogs.SelectMany(s => s.LogRecords).ToList();
            // Expect exactly 2 log records: the AppException + the AppTrace. The AppRequest and
            // the unsupported AppPageViews must not appear here.
            Assert.AreEqual(2, logs.Count, "Only AppExceptions and AppTraces should produce log records.");

            var exceptionLog = logs.Single(l => l.SeverityNumber == SeverityNumber.Error);
            Assert.AreEqual("ExceptionMessageOnLogsSide", exceptionLog.Body.StringValue);
            Assert.AreEqual("System.InvalidOperationException",
                exceptionLog.Attributes.First(a => a.Key == "exception.type").Value.StringValue);

            var traceLog = logs.Single(l => l.SeverityNumber == SeverityNumber.Warn);
            Assert.AreEqual("TraceMessageOnLogsSide", traceLog.Body.StringValue);
        }

        [Test]
        public void ConvertAppExceptions2OTLP()
        {
            string json = File.ReadAllText("samples/AppExceptions.json");
            var conv = new ApplicationInsights2OTLP.Convert(null);
            var req = conv.FromApplicationInsights(json).Logs;

            Assert.IsNotNull(req);
            Assert.AreEqual(1, req.ResourceLogs.Count);
            var resLogs = req.ResourceLogs[0];

            // resource attributes
            Assert.AreEqual("exceptiontest",
                resLogs.Resource.Attributes.First(a => a.Key == "service.name").Value.StringValue);

            // instrumentation scope
            Assert.AreEqual(1, resLogs.ScopeLogs.Count);
            Assert.IsTrue(resLogs.ScopeLogs[0].Scope.Name.StartsWith("azurefunctions"));

            var logs = resLogs.ScopeLogs[0].LogRecords;
            Assert.AreEqual(1, logs.Count);
            var log = logs[0];

            // correlation ids
            Assert.AreEqual(16, log.TraceId.Length);
            Assert.AreEqual(8, log.SpanId.Length);

            // severity
            Assert.AreEqual(SeverityNumber.Error, log.SeverityNumber);
            Assert.AreEqual("ERROR", log.SeverityText);

            // body and OTel exception attributes
            Assert.AreEqual("Outer boom", log.Body.StringValue);

            var getAttr = new Func<string, string?>(key =>
            {
                var a = log.Attributes.FirstOrDefault(x => x.Key == key);
                return a == null ? null : a.Value.StringValue;
            });
            Assert.AreEqual("System.InvalidOperationException", getAttr("exception.type"));
            Assert.AreEqual("Outer boom", getAttr("exception.message"));

            var stack = getAttr("exception.stacktrace");
            Assert.IsNotNull(stack);
            // Outer + inner exception frames should both appear, with file/line info.
            StringAssert.Contains("Sample.Failing.Do", stack!);
            StringAssert.Contains("/src/Failing.cs:42", stack);
            StringAssert.Contains("Sample.Failing.Validate", stack);
            StringAssert.Contains("--->", stack);

            Assert.AreEqual("AppExceptions", getAttr("appinsights.type"));
            Assert.AreEqual("System.InvalidOperationException at Sample.Failing.Do",
                getAttr("appinsights.exception.problem_id"));
            Assert.AreEqual("UserCode", getAttr("appinsights.exception.handled_at"));

            Assert.Greater(log.TimeUnixNano, 0UL);
        }

        [Test]
        public void ConvertAppTraces2OTLP()
        {
            string json = File.ReadAllText("samples/AppTraces.json");
            var conv = new ApplicationInsights2OTLP.Convert(null);
            var req = conv.FromApplicationInsights(json).Logs;

            Assert.IsNotNull(req);
            var resLogs = req.ResourceLogs[0];
            Assert.AreEqual("tracetest",
                resLogs.Resource.Attributes.First(a => a.Key == "service.name").Value.StringValue);

            var logs = resLogs.ScopeLogs[0].LogRecords;
            Assert.AreEqual(1, logs.Count);
            var log = logs[0];

            // severity 2 in AppInsights == WARN in OTel
            Assert.AreEqual(SeverityNumber.Warn, log.SeverityNumber);
            Assert.AreEqual("WARN", log.SeverityText);

            Assert.AreEqual("TraceMessageThatShouldSurvive", log.Body.StringValue);
            Assert.AreEqual("AppTraces",
                log.Attributes.First(a => a.Key == "appinsights.type").Value.StringValue);

            // correlation
            Assert.AreEqual(16, log.TraceId.Length);
            Assert.AreEqual(8, log.SpanId.Length);
        }

        [Test]
        public void ProducerInfoEmittedAsResourceAttributes()
        {
            string json = File.ReadAllText("samples/AppRequests.json");
            var ua = "ApplicationInsightsForwarder/9.9.9 (+https://github.com/dtPaTh/ApplicationInsightsExporter)";
            var conv = new ApplicationInsights2OTLP.Convert(null, ua);
            var req = conv.FromApplicationInsights(json).Traces;

            var resAttrs = req.ResourceSpans[0].Resource.Attributes;
            Assert.AreEqual(ua, resAttrs.First(a => a.Key == "appinsightsforwarder.useragent").Value.StringValue);
        }

        [Test]
        public void ProducerInfoOmittedWhenNotProvided()
        {
            string json = File.ReadAllText("samples/AppRequests.json");
            var conv = new ApplicationInsights2OTLP.Convert(null);
            var req = conv.FromApplicationInsights(json).Traces;

            var resAttrs = req.ResourceSpans[0].Resource.Attributes;
            Assert.IsFalse(resAttrs.Any(a => a.Key == "appinsightsforwarder.useragent"));
        }

        [Test]
        public void TryConvertInvalidJson()
        {
            string json = File.ReadAllText("samples/Invalid.json");
            var conv = new ApplicationInsights2OTLP.Convert(null);

            // Invalid.json does not contain the expected 'records' array and should throw when parsing
            Assert.That(() => conv.FromApplicationInsights(json), Throws.InstanceOf<ArgumentException>());
        }


        [Test]
        public void TryConvertInvalidText()
        {
            string json = File.ReadAllText("samples/Invalid.txt");
            var conv = new ApplicationInsights2OTLP.Convert(null);

            // Invalid.txt does not contain a json string and should throw when parsing
            Assert.That(() => conv.FromApplicationInsights(json), Throws.InstanceOf<System.Text.Json.JsonException>());
        }

    }
}