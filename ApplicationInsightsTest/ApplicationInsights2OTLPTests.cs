using NUnit.Framework;
using Opentelemetry.Proto.Trace.V1;
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
            var req = conv.FromApplicationInsights(json);

            Assert.IsNotNull(req);
            Assert.IsNotNull(req.ResourceSpans);
            Assert.AreEqual(1, req.ResourceSpans.Count, "Expected exactly one ResourceSpans entry");

            var resSpan = req.ResourceSpans[0];
            // resource attributes
            var serviceNameAttr = resSpan.Resource.Attributes.FirstOrDefault(a => a.Key == "service.name");
            Assert.IsNotNull(serviceNameAttr);
            Assert.AreEqual("apprequesttest", serviceNameAttr.Value.StringValue);

            // instrumentation library
            Assert.IsNotNull(resSpan.InstrumentationLibrarySpans);
            Assert.AreEqual(1, resSpan.InstrumentationLibrarySpans.Count);
            var lib = resSpan.InstrumentationLibrarySpans[0].InstrumentationLibrary;
            Assert.IsNotNull(lib);
            Assert.IsTrue(lib.Name.StartsWith("azurefunctions"), "Expected instrumentation library name to start with 'azurefunctions'");

            // spans
            var spans = resSpan.InstrumentationLibrarySpans[0].Spans;
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
        }

        [Test]
        public void ConvertAppDependencies2OTLP()
        {
            string json = File.ReadAllText("samples/AppDependencies.json");
            var conv = new ApplicationInsights2OTLP.Convert(null);
            var req = conv.FromApplicationInsights(json);

            Assert.IsNotNull(req);
            Assert.IsNotNull(req.ResourceSpans);
            Assert.AreEqual(1, req.ResourceSpans.Count);

            var resSpan = req.ResourceSpans[0];
            var spans = resSpan.InstrumentationLibrarySpans[0].Spans;
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

            // http semantic attrs
            Assert.AreEqual("GET", getAttr("http.method"));
            Assert.AreEqual("/api/Receiver", getAttr("http.target"));
            Assert.AreEqual("https://someotherservice.urewebsites.net/api/Receiver", getAttr("http.url"));
            Assert.AreEqual("200", getAttr("http.status_code"));

            Assert.Greater(span.StartTimeUnixNano, 0UL);
            Assert.Greater(span.EndTimeUnixNano, span.StartTimeUnixNano);
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