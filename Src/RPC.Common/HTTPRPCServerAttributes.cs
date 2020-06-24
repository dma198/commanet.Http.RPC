using System;

namespace commanet.Http.RPC
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class HTTPRPCInterfaceAttribute : Attribute
    {        
        public string BasePath { get; set; }
        public bool AddInterfaceNameToPath { get; set; }
        public HTTPRPCInterfaceAttribute(string BasePath = "api", bool AddInterfaceNameToPath = false)
        {
            this.BasePath = BasePath;
            this.AddInterfaceNameToPath = AddInterfaceNameToPath;
        }
    }

    public enum ParamsPlacedIn {URLPath, Query, Body}

    [AttributeUsage(AttributeTargets.Method)]
    public class HTTPHandlerAttribute : Attribute
    {
        public HttpMethods Method { get; set; }
        public int CacheMilliseconds { get; set; }
        public string Path { get; set; }
        public ParamsPlacedIn ParametersIn { get; set; }
      
        public HTTPHandlerAttribute(string Path = "", HttpMethods Method = HttpMethods.Post,
                                    ParamsPlacedIn ParametersIn = ParamsPlacedIn.URLPath,  int CacheMilliseconds = 0)
        {
            this.Method = Method;
            this.CacheMilliseconds = CacheMilliseconds;
            this.Path = Path;
            this.ParametersIn = ParametersIn;
        }

    }

}

