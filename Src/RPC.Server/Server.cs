using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Net;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace commanet.Http.RPC
{
    public partial class HTTPRPCServer : IDisposable
    {

        private static readonly  Regex rxSplitArgs = new Regex("/(?=%22|%7B|[\\d]+|false|true|null)(?!%22(/|$))");

        private class HttpHandler
        {
            public string? path;
            public HttpMethods httpMethod;
            public int CacheMilliseconds;
            public object? obj;
            public MethodInfo? m;
            public Type? MethodRequestType;
            public Type? MethodResponseType;
            public bool BinaryResult;
        }
        private static readonly List<HttpHandler> handlers = new List<HttpHandler>();

        private readonly IWebHost host;
        public HTTPRPCServer(string addresses= "*",int port=5000,
            int MaxRequestLineSize=8192,
            long MaxRequestBodySize = 1048576,
            long MaxRequestBufferSize=1048576,
            long MaxResponseBufferSize=65536)
        {
            host = new WebHostBuilder()
                .UseKestrel(options => {
                    options.Limits.MaxRequestBodySize = MaxRequestBodySize;
                    options.Limits.MaxRequestBufferSize = MaxRequestBufferSize;
                    options.Limits.MaxResponseBufferSize = MaxResponseBufferSize;
                    options.Limits.MaxRequestLineSize = MaxRequestLineSize;
                })
                .UseSetting(WebHostDefaults.SuppressStatusMessagesKey, "True")
                .UseStartup<Startup>()
                .ConfigureKestrel((context,options) => 
                {
                    if(addresses=="*")
                        options.ListenAnyIP(port);
                    else if (addresses.Trim().ToUpperInvariant() == "LOCALHOST")
                        options.ListenLocalhost(port);
                    else
                    {
                        var tmp = addresses.Split(',');
                        foreach(var addr in tmp)
                        {
                            try
                            {
                                var ipAddr = IPAddress.Parse(addr);
                                options.Listen(ipAddr, port);
                            }
                            #pragma warning disable CA1031 // Do not catch general exception types
                            catch (Exception) { }
                            #pragma warning restore CA1031 // Do not catch general exception types
                        }
                    }
                })
                .Build();
        }
 
        public void Run()
        {
            Utils.Init();


            var rpcClasses =
                from a in AppDomain.CurrentDomain.GetAssemblies().AsParallel()
                where !a.IsDynamic
                from t in a.GetTypes()
                from i in t.GetInterfaces()
                let attributes = i.GetCustomAttributes(typeof(HTTPRPCInterfaceAttribute), true)
                where attributes != null && attributes.Length > 0
                select t;

            foreach (var rpcClass in rpcClasses)
            {
                bool addInterfaceNameToPath = false;
                var basePath = "api";
                HTTPRPCInterfaceAttribute? aServer = null;
                foreach (var i in rpcClass.GetInterfaces())
                {
                    aServer = i.GetCustomAttribute<HTTPRPCInterfaceAttribute>();
                    if (aServer != null) break;
                }

                if (aServer != null)
                {
                    addInterfaceNameToPath = aServer.AddInterfaceNameToPath;
                    basePath = aServer.BasePath;
                }

                var obj = Activator.CreateInstance(rpcClass);
                if (obj != null)
                {
                    var t = obj.GetType().GetInterfaces()[0];
                    MethodInfo[] procs = t.GetMethods();

                    foreach (var m in procs)
                    {
                        var aHTTP = m.GetCustomAttribute<HTTPHandlerAttribute>();
                        var lHttpMethod = HttpMethods.Post;
                        var lCacheMilliseconds = 0;
                        string lPath = "";
                        if (aHTTP != null)
                        {
                            lHttpMethod = aHTTP.Method;
                            lCacheMilliseconds = aHTTP.CacheMilliseconds;
                            lPath += "/" + aHTTP.Path;
                        }

                        var mName = m.Name.Trim().ToUpperInvariant();
                        var path = mName;

                        if (addInterfaceNameToPath)
                        {
                            string iName = Utils.StripInterfaceName(t.Name).Trim().ToUpperInvariant();
                            if (iName != mName) path = iName + "/" + path;
                        }

                        path = "/" + path;
                        if (!string.IsNullOrEmpty(lPath.Trim().Trim('/')))
                            path = "/" + lPath.Trim('/') + "/" + path.Trim('/');
                        if (basePath != null && !string.IsNullOrEmpty(basePath.Trim().Trim('/')))
                            path = "/" + basePath.Trim('/') + path;

                        handlers.Add(new HttpHandler()
                        {
                            path = path.ToUpperInvariant(),
                            httpMethod = lHttpMethod,
                            CacheMilliseconds = lCacheMilliseconds,
                            obj = obj,
                            m = m,
                            MethodRequestType = Utils.GetMethodRequestType(t, m.Name),
                            MethodResponseType = (m.ReturnType == typeof(RPCBinaryResult)) ? null : Utils.GetMethodResponseHolderType(t.Name + "/" + m.Name, m.ReturnType),
                            BinaryResult = (m.ReturnType == typeof(RPCBinaryResult))
                        });
                    }

                }
            }
//            Task.Delay(10000).ContinueWith(o=>{IsStarted = true;}); // Wait 10 sec t be sure that host is started and then change IsStarted status
            host.Start();
            IsStarted = true;
        }

        public bool IsStarted{get;private set;} = false;

        public void WaitForShutdown()
        {
            host.WaitForShutdown();
        }

        private bool isDisposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed) return;

            if (disposing)
            {
                host?.Dispose();
            }

            isDisposed = true;
        }

    }
}
