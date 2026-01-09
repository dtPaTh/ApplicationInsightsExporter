using NUnit.Framework;
using Opentelemetry.Proto.Trace.V1;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ApplicationInsightsForwarderWorker;

namespace ApplicationInsightsTest
{


    public class ApplicationInsightsForwarderWorkerTest
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
        public void CreatesOtlpEndpoint_AppendsV1Traces_WhenMissing()
        {
            var cfg = new ForwarderConfig("http://example.com");
            Assert.AreEqual("http://example.com/v1/traces", cfg.OTLPEndpoint);
        }

        [Test]
        public void CreatesOtlpEndpoint_AppendsV1Traces_WhenTrailingSlash()
        {
            var cfg = new ForwarderConfig("http://example.com/");
            Assert.AreEqual("http://example.com/v1/traces", cfg.OTLPEndpoint);
        }

        [Test]
        public void CreatesOtlpEndpoint_LeavesAsIs_WhenContainsV1Traces()
        {
            var cfg = new ForwarderConfig("http://example.com/v1/traces");
            Assert.AreEqual("http://example.com/v1/traces", cfg.OTLPEndpoint);
        }

        [Test]
        public void CreatesOtlpEndpoint_DefaultsToLocalhost_WhenNullOrEmpty()
        {
            var cfg = new ForwarderConfig(null);
            Assert.AreEqual("http://localhost:4318/v1/traces", cfg.OTLPEndpoint);
        }

    }
}