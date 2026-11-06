using System.Net;
using System.Net.Http.Headers;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    // A job whose upload stops part way until it is released. Its first bytes go at once, so that the server has the
    // request and has given it a job's place; the rest go when the test says.
    internal sealed class HeldContent
        : HttpContent
    {
        private const int c_FirstBytes = 16;

        private readonly byte[] m_Body;
        private readonly TaskCompletionSource m_Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource m_Released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private HeldContent(byte[] body, MediaTypeHeaderValue? contentType)
        {
            m_Body = body;
            Headers.ContentType = contentType;
        }

        public Task Started => m_Started.Task;

        public static async Task<HeldContent> CreateAsync(MultipartFormDataContent job)
        {
            using (job)
            {
                return new HeldContent(await job.ReadAsByteArrayAsync(), job.Headers.ContentType);
            }
        }

        public void Release()
        {
            m_Released.TrySetResult();
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return SerializeToStreamAsync(stream, context, CancellationToken.None);
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            await stream.WriteAsync(m_Body.AsMemory(0, c_FirstBytes), cancellationToken);
            await stream.FlushAsync(cancellationToken);
            m_Started.TrySetResult();

            await m_Released.Task.WaitAsync(cancellationToken);
            await stream.WriteAsync(m_Body.AsMemory(c_FirstBytes), cancellationToken);
        }

        protected override bool TryComputeLength(out long length)
        {
            // Sent in chunks, so that its first bytes go on their own.
            length = 0;
            return false;
        }
    }
}
