namespace ApplicationInsightsForwarderWorker
{
    internal static class ForwarderInfo
    {
        public const string Name = "ApplicationInsightsForwarder";
        public const string Version = "1.2.0";
        public const string Url = "https://github.com/dtPaTh/ApplicationInsightsExporter";

        public static readonly string UserAgent = $"{Name}/{Version} (+{Url})";
    }
}
