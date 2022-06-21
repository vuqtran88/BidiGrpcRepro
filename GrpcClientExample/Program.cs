using Grpc.Core;
using Grpc.Net.Client;
using NewRelic.Agent.Core.Segments;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GrpcClientExample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Client. Press any key to start.");
            while (Console.ReadKey().Key != ConsoleKey.Escape)
            {
                await Stream(60000);
            }
        }

        public static async Task Stream(int timeoutMs)
        {

            var grpcOption = new GrpcChannelOptions();

            grpcOption.Credentials = new SslCredentials();

#if NETFRAMEWORK

            grpcOption.HttpHandler = new WinHttpHandler();

#endif

            using var channel = GrpcChannel.ForAddress("https://localhost:5005", grpcOption);
            var client = new NewRelic.Agent.Core.Segments.IngestService.IngestServiceClient(channel);

            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

            using var streams = client.RecordSpan(cancellationToken: cts.Token);

            var requestStream = streams.RequestStream;
            var responseStream = streams.ResponseStream;

            var tasks = new List<Task>();

            var count = 0;


            tasks.Add(Task.Run(async () =>
            {

                while (!cts.IsCancellationRequested)
                {
                    try
                    {

                        var span = new Span();

                        await requestStream.WriteAsync(span);

                        count++;

                        if (count % 50 == 0)
                        {
                            Console.WriteLine($"Sent: 50 spans");
                            Thread.Sleep(5000);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }

                await requestStream.CompleteAsync();
            }));

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var success = await responseStream.MoveNext(cts.Token);
                    while (success)
                    {
                        var status = responseStream.Current;

                        Console.WriteLine($"Server Received: {status.MessagesSeen}");

                        success = await responseStream.MoveNext(cts.Token);
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    Console.WriteLine(ex.ToString());
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }));

            tasks.Add(Task.Delay(TimeSpan.FromSeconds(timeoutMs)));

            await Task.WhenAny(tasks);
        }

    }
}
