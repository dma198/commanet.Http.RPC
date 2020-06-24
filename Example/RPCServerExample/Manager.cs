// It does:
//  - Create and run HTTPRPCServer in thread 
//  - Create HTTPRPCClient and call server methods by timer 

using System;
using System.Threading;
using System.Diagnostics;
using System.Globalization;

using Microsoft.Extensions.Configuration;

using commanet;
using commanet.Http.RPC;

namespace RPCServerExample
{
    public class Manager : ManagerBase
    {
        public override string Description => "HTTPRPCServer Example";

        private static int cnt = 0;
        private static Timer? timer;
        private static HTTPRPCServer? server; 

        public override bool Startup(ApplicationBase app,IConfiguration? config)
        {
            Logger?.Info("Starting HTTP Server");
            server = new HTTPRPCServer("localhost", 5010);
            server.Run();
            
            Logger?.Info("Starting Client Sendings by Timer");
            var cli = HTTPRPCClient.Client<IHttpAPI>(new Uri("http://localhost:5010/api"));
            var inprogress = false;
            timer = new Timer((st) => {
                if (inprogress) return;
                inprogress = true;
                try
                {
                    cnt++;
                    var sw = Stopwatch.StartNew(); 
                    var r = cli.Test(cnt);
                    Logger?.Info($"RPC Call Executed in {sw.ElapsedMilliseconds} ms. Result: {r.ToString("HH:mm:ss",CultureInfo.InvariantCulture)}");
                }
                #pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
                {
                    Logger?.Error(ex.Message);
                }
                #pragma warning restore CA1031 // Do not catch general exception types
                inprogress = false;
            }, null, 5000, 5000);
            
            return true;
        }

        public override void Shutdown()
        {
            timer?.Dispose();
            server?.Dispose();
        }
    }

    public class DataExample
    {
        public string Test { get; set; } = "";
    }

    [HTTPRPCInterface]
    public interface IHttpAPI
    {
        [HTTPHandler(Method = HttpMethods.Get)]
        DateTime Test(int cnt);

        [HTTPHandler(Method = HttpMethods.Get)]
        string Echo(string s);

        [HTTPHandler(Method = HttpMethods.Get)]
        byte[] Binary();
    }

    public class HttpServer : IHttpAPI
    {
        public DateTime Test(int cnt)
        {
            return DateTime.Now;//new Data() { Test = string.Format("Hello HTTPRPC: {0}",cnt)};
        }

        public string Echo(string s)
        {
            return s;
        }

        public byte[] Binary()
        {
            return new byte[] {1,2,3,4,5,6,7,8,9};
        }
    }

}
