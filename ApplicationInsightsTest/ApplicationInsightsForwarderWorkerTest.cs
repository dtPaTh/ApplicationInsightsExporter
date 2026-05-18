using NUnit.Framework;
using ApplicationInsightsForwarderWorker;

namespace ApplicationInsightsTest
{
    public class ApplicationInsightsForwarderWorkerTest
    {
        [SetUp]
        public void Setup() { }

        [TearDown]
        public void Teardown() { }

        // --- Bare base URL fallback (OTLP_ENDPOINT only) ---

        [Test]
        public void BareBase_DerivesBothTracesAndLogs()
        {
            var cfg = new ForwarderConfig(null, null, "http://example.com");
            Assert.AreEqual("http://example.com/v1/traces", cfg.TracesEndpoint);
            Assert.AreEqual("http://example.com/v1/logs", cfg.LogsEndpoint);
        }

        [Test]
        public void BareBase_TrailingSlash_DerivesBothTracesAndLogs()
        {
            var cfg = new ForwarderConfig(null, null, "http://example.com/");
            Assert.AreEqual("http://example.com/v1/traces", cfg.TracesEndpoint);
            Assert.AreEqual("http://example.com/v1/logs", cfg.LogsEndpoint);
        }

        // --- Back-compat: OTLP_ENDPOINT with a signal-specific path ---

        [Test]
        public void LegacyTracesUrl_KeepsTracesOnly_LogsRemainsNull()
        {
            var cfg = new ForwarderConfig(null, null, "http://example.com/v1/traces");
            Assert.AreEqual("http://example.com/v1/traces", cfg.TracesEndpoint);
            Assert.IsNull(cfg.LogsEndpoint);
        }

        [Test]
        public void LegacyLogsUrl_KeepsLogsOnly_TracesFallsBackToDefault()
        {
            var cfg = new ForwarderConfig(null, null, "http://example.com/v1/logs");
            Assert.AreEqual("http://localhost:4318/v1/traces", cfg.TracesEndpoint);
            Assert.AreEqual("http://example.com/v1/logs", cfg.LogsEndpoint);
        }

        // --- Explicit endpoints win over base ---

        [Test]
        public void ExplicitTraces_OverridesBase()
        {
            var cfg = new ForwarderConfig("http://traces.example.com", null, "http://base.example.com");
            Assert.AreEqual("http://traces.example.com/v1/traces", cfg.TracesEndpoint);
            Assert.AreEqual("http://base.example.com/v1/logs", cfg.LogsEndpoint);
        }

        [Test]
        public void ExplicitLogs_OverridesBase()
        {
            var cfg = new ForwarderConfig(null, "http://logs.example.com", "http://base.example.com");
            Assert.AreEqual("http://base.example.com/v1/traces", cfg.TracesEndpoint);
            Assert.AreEqual("http://logs.example.com/v1/logs", cfg.LogsEndpoint);
        }

        [Test]
        public void ExplicitTracesAndLogs_BothUsed_NoBase()
        {
            var cfg = new ForwarderConfig("http://traces.example.com", "http://logs.example.com", null);
            Assert.AreEqual("http://traces.example.com/v1/traces", cfg.TracesEndpoint);
            Assert.AreEqual("http://logs.example.com/v1/logs", cfg.LogsEndpoint);
        }

        [Test]
        public void ExplicitTraces_AppendsV1Traces_WhenMissing()
        {
            var cfg = new ForwarderConfig("http://example.com", null, null);
            Assert.AreEqual("http://example.com/v1/traces", cfg.TracesEndpoint);
            Assert.IsNull(cfg.LogsEndpoint);
        }

        [Test]
        public void ExplicitTraces_LeavesAsIs_WhenContainsV1Traces()
        {
            var cfg = new ForwarderConfig("http://example.com/v1/traces", null, null);
            Assert.AreEqual("http://example.com/v1/traces", cfg.TracesEndpoint);
        }

        // --- Defaults ---

        [Test]
        public void NoEndpointsConfigured_TracesDefaultsToLocalhost_LogsNull()
        {
            var cfg = new ForwarderConfig(null, null, null);
            Assert.AreEqual("http://localhost:4318/v1/traces", cfg.TracesEndpoint);
            Assert.IsNull(cfg.LogsEndpoint);
        }

        // --- Authorization header resolution ---

        [Test]
        public void BaseAuthHeader_AppliesToBothSignals()
        {
            var cfg = new ForwarderConfig(
                tracesEndpoint: null, logsEndpoint: null, baseEndpoint: "http://example.com",
                baseAuthHeader: "Api-Token shared");
            Assert.AreEqual("Api-Token shared", cfg.TracesAuthHeader);
            Assert.AreEqual("Api-Token shared", cfg.LogsAuthHeader);
        }

        [Test]
        public void ExplicitTracesAuthHeader_OverridesBase_LogsKeepsBase()
        {
            var cfg = new ForwarderConfig(
                tracesEndpoint: null, logsEndpoint: null, baseEndpoint: "http://example.com",
                tracesAuthHeader: "Api-Token traces",
                baseAuthHeader: "Api-Token shared");
            Assert.AreEqual("Api-Token traces", cfg.TracesAuthHeader);
            Assert.AreEqual("Api-Token shared", cfg.LogsAuthHeader);
        }

        [Test]
        public void ExplicitLogsAuthHeader_OverridesBase_TracesKeepsBase()
        {
            var cfg = new ForwarderConfig(
                tracesEndpoint: null, logsEndpoint: null, baseEndpoint: "http://example.com",
                logsAuthHeader: "Api-Token logs",
                baseAuthHeader: "Api-Token shared");
            Assert.AreEqual("Api-Token shared", cfg.TracesAuthHeader);
            Assert.AreEqual("Api-Token logs", cfg.LogsAuthHeader);
        }

        [Test]
        public void ExplicitTracesAndLogsAuthHeaders_NoBase_BothUsed()
        {
            var cfg = new ForwarderConfig(
                tracesEndpoint: "http://traces.example.com", logsEndpoint: "http://logs.example.com", baseEndpoint: null,
                tracesAuthHeader: "Api-Token traces",
                logsAuthHeader: "Api-Token logs");
            Assert.AreEqual("Api-Token traces", cfg.TracesAuthHeader);
            Assert.AreEqual("Api-Token logs", cfg.LogsAuthHeader);
        }

        [Test]
        public void NoAuthHeadersConfigured_BothNull()
        {
            var cfg = new ForwarderConfig("http://example.com", null, null);
            Assert.IsNull(cfg.TracesAuthHeader);
            Assert.IsNull(cfg.LogsAuthHeader);
        }
    }
}
