using Google.Protobuf;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;


namespace ApplicationInsights2OTLP
{
    //https://github.com/open-telemetry/opentelemetry-dotnet/blob/1a2103ddc10e9b80cd45d24b85083aba9738e759/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/ExportClient/OtlpHttpTraceExportClient.cs#L42
    public class ExportRequestContent : HttpContent
    {
        internal const string MediaContentType = "application/x-protobuf";

        private static readonly MediaTypeHeaderValue ProtobufMediaTypeHeader = new MediaTypeHeaderValue(MediaContentType);

        private readonly IMessage exportRequest;

        public ExportRequestContent(IMessage exportRequest)
        {
            this.exportRequest = exportRequest;
            this.Headers.ContentType = ProtobufMediaTypeHeader;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            this.SerializeToStreamInternal(stream);
            return Task.CompletedTask;
        }

        protected override bool TryComputeLength(out long length)
        {
            // We can't know the length of the content being pushed to the output stream.
            length = -1;
            return false;
        }

        private void SerializeToStreamInternal(Stream stream)
        {
            this.exportRequest.WriteTo(stream);
        }
    }
}
