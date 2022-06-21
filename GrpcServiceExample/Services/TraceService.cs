using Grpc.Core;
using Microsoft.Extensions.Logging;
using NewRelic.Agent.Core.Segments;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GrpcServiceExample
{
	public class TraceService : IngestService.IngestServiceBase
	{
		private static int _channelID = 0;
		public override async Task RecordSpan(IAsyncStreamReader<Span> requestStream, IServerStreamWriter<RecordStatus> responseStream, ServerCallContext context)
		{
			var channelID = Interlocked.Increment(ref _channelID);

			Console.WriteLine($"Channel #{channelID}: Created");

			var spanCount = 0;

            void statsWriterWorker()
            {
                var statVal = Interlocked.Exchange(ref spanCount, 0);
                if (statVal == 0)
                {
                    return;
                }
                Console.WriteLine($"Channel #{channelID}: Stats Output: Count Spans: {statVal}");
                responseStream.WriteAsync(new RecordStatus() { MessagesSeen = (ulong)statVal });
            }

            var expireDtm = DateTime.Now.Add(TimeSpan.FromSeconds(60));

			while (await requestStream.MoveNext(context.CancellationToken))
			{
				//pretending busy
				Thread.Sleep(100);

				var x = Interlocked.Increment(ref spanCount);
				var span = requestStream.Current;

				// do something with span
				// ...
				// ...

				//Reply every 50 spans
				if (x % 50 == 0)
				{
					statsWriterWorker();
				}
			}

			Console.WriteLine($"Channel #{channelID}: ending");

			await Task.CompletedTask;
		}

	}
}
