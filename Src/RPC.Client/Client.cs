using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

namespace commanet.Http.RPC
{

    public static class HTTPRPCClient
    {
        public static int WaitResponseTimeoutMs { get; set; } = 30000;
        public static int ReadContentTimeoutMs { get; set; } = 5000;

        public static bool IgnoreServerCertificateValidationError { get; set; } = false; 
        //private static readonly List<HttpHandler> handlers = new List<HttpHandler>();

        public static NetworkCredential? Credentials { get; set; } = null; 

        public static object Call(string PrcName, Uri url, string httpMethod, object args, Type argType, ParamsPlacedIn  paramsPlaced,Type resType)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));

            object? res = null;
            string sguid = Guid.NewGuid().ToString();

            Exception? ex = null;

            HttpClient cli;

            using var handler = new HttpClientHandler();
            
            if (IgnoreServerCertificateValidationError)
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

            if (Credentials!=null)
            {
                handler.Credentials = Credentials ;
                cli = new HttpClient(handler);
            }
            else
            {
                cli = new HttpClient(handler);
            }

            using (cli)
            {

                cli.DefaultRequestHeaders.Accept.Clear();
                cli.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                Task<HttpResponseMessage>? tsk = null;
                
                #pragma warning disable CA1308 // Normalize strings to uppercase
                var lUrl = new Uri((url + "/" + PrcName).ToLowerInvariant());
                #pragma warning restore CA1308 // Normalize strings to uppercase
                string urlArgs = "";
                StringContent? content = null;
                try
                {                                       
                    // Pass parameters for Get request in URL
                    if (httpMethod == HttpMethods.Get.ToString().ToUpperInvariant())
                    {
                        if (paramsPlaced == ParamsPlacedIn.URLPath && argType!=null)
                        {
                            foreach (var p in argType.GetProperties())
                            {
                                string val = JsonSerializer.Serialize(p.GetValue(args));
                                if (p.PropertyType == typeof(string))
                                    val = val.Replace("\\", Constants.BACKSLASHJOKER,StringComparison.Ordinal);
                                val = Uri.EscapeUriString(val);
                                urlArgs += val + "/";
                            }
                            if (!string.IsNullOrEmpty(urlArgs))
                            {
                                urlArgs = "/" + urlArgs.Trim('/');                                
                                lUrl = new Uri(lUrl.AbsoluteUri + urlArgs);
                            }
                        }
                        else if (paramsPlaced == ParamsPlacedIn.Query && argType != null)
                        {
                            var prps = argType.GetProperties();
                            if (prps != null && prps.Length > 0)
                            {
                                var query = "?";
                                for (int i = 0; i < prps.Length; i++)
                                {
                                    string val = JsonSerializer.Serialize(prps[i].GetValue(args));
                                    if (prps[i].PropertyType == typeof(string))
                                        val = val.Replace("\\", Constants.BACKSLASHJOKER, StringComparison.Ordinal);
                                    val = Uri.EscapeUriString(val);
                                    // Escape does not work for + character because according standard
                                    // specification + is legal character in url used as alias for space
                                    val = val.Replace("+", "%2B",StringComparison.Ordinal); // Here we manually escape plus
                                    #pragma warning disable CA1308 // Normalize strings to uppercase
                                    query += prps[i].Name.ToLowerInvariant() + "=" + val;
                                    #pragma warning restore CA1308 // Normalize strings to uppercase
                                    if (i < prps.Length - 1) query += "&";
                                }
                                var qBld = new UriBuilder(lUrl)
                                {
                                    Query = query
                                };
                                lUrl = qBld.Uri;
                            }
                        }
                        else
                        {
                            #pragma warning disable CA1303 // Do not pass literals as localized parameters
                            var exception = new Exception("Method attributed as used GET http request. Parameters for such requests can be passed only in URL or in QUERY but not in BODY");
                            #pragma warning restore CA1303 // Do not pass literals as localized parameters
                            throw exception;
                        }

                        tsk = cli.GetAsync(lUrl);
                    }
                    else if (httpMethod == HttpMethods.Post.ToString().ToUpperInvariant())
                    {
                        var rs=JsonSerializer.Serialize(args,args.GetType());
                        content = new StringContent(JsonSerializer.Serialize(args), Encoding.UTF8, "application/json");
                        tsk = cli.PostAsync(lUrl, content);
                    }
                    else if (httpMethod == HttpMethods.Put.ToString().ToUpperInvariant())
                    {
                        content = new StringContent(JsonSerializer.Serialize(args), Encoding.UTF8, "application/json");
                        tsk = cli.PutAsync(lUrl, content);
                    }
                    else if (httpMethod == HttpMethods.Delete.ToString().ToUpperInvariant())
                    {
                        content = new StringContent(JsonSerializer.Serialize(args), Encoding.UTF8, "application/json");
                        tsk = cli.PutAsync(lUrl, content);
                    }
                    else
                    {
                        throw new Exception("Unknown HTTP METHOD: " + httpMethod);
                    }

                    var clientError = string.Empty;
                    var isTimout = !tsk.Wait(WaitResponseTimeoutMs);
                    if(isTimout)
                    {
                        clientError = $"Wait server response exceed timeout ({WaitResponseTimeoutMs} ms)";
                    }

                    content?.Dispose();

                    var hasError = isTimout || tsk == null || !tsk.Result.IsSuccessStatusCode;

                    if (!hasError && tsk!=null)
                    {
                        var contentType = tsk.Result.Content.Headers.ContentType;
                        if (contentType.MediaType=="text/html" || contentType.MediaType == "application/json")
                        {
                            var ltsk = tsk.Result.Content.ReadAsStringAsync();
                            if (ltsk.Wait(ReadContentTimeoutMs))
                                res = JsonSerializer.Deserialize(ltsk.Result, resType);
                            else
                            {
                                clientError = $"Read response content exceed timeout ({ReadContentTimeoutMs} ms)";
                                hasError = true;
                            }

                        }
                        /*
                        if (resType != typeof(HTTPBinaryResult))
                        {
                            var ltsk = tsk.Result.Content.ReadAsStringAsync();
                            ltsk.Wait();
                            json = ltsk.Result;
                            var resp = JsonConvert.DeserializeObject<RPCResponse>(json);
                            var responseType = Utils.GetMethodResponseHolderType(PrcName, resType);
                            if (resp.res is Newtonsoft.Json.Linq.JObject && resType.IsClass)
                            {
                                res = ((Newtonsoft.Json.Linq.JObject)resp.res).ToObject(resType);
                            }
                            else    
                                res = resp.res;
                            //else res= JsonConvert.DeserializeObject(json,restype);
                        }
                        else // Manage Binary Result
                        {
                            var ltsk = tsk.Result.Content.ReadAsByteArrayAsync();
                            ltsk.Wait();
                            HTTPBinaryResult binres = new HTTPBinaryResult()
                            {
                                Data = ltsk.Result
                            };
                            if (tsk.Result.Headers.Contains(Constants.HEADER_CONTINUE_TOKEN))
                                binres.ContinueToken = tsk.Result.Headers.GetValues(Constants.HEADER_CONTINUE_TOKEN).First();
                            res = binres;
                        }
                        */
                    }
                    if(hasError)
                    {
                        if(!string.IsNullOrEmpty(clientError))
                        {
                            throw new Exception($"Http client error: {clientError}");
                        }
                        var ServerReason = "Unknown";
                        if (tsk!=null && tsk.Result.StatusCode == HttpStatusCode.InternalServerError)
                        {
                            var ltsk = tsk.Result.Content.ReadAsStringAsync();
                            if(ltsk.Wait(ReadContentTimeoutMs))
                                ServerReason=ltsk.Result;
                            else
                                ServerReason = "Unable to get server side reason. Timeout ({ReadContentTimeoutMs} ms) happens when tried to read server response content";
                        }
                        var statusCode = tsk != null ? tsk.Result.StatusCode : HttpStatusCode.NoContent;
                        throw new Exception($"Http non-successful result: StatusCode:{statusCode}, ServerReason: {ServerReason}");
                    }
                }
                #pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex1)
                {
                    Exception lex = ex1;
                    while (lex.InnerException != null)
                        lex = lex.InnerException;
                    ex = lex;
                }
                #pragma warning restore CA1031 // Do not catch general exception types

                if (ex != null) throw ex;
            }

            if(res==null)
            {
                #pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Can't get result from remote call");
                #pragma warning restore CA1303 // Do not pass literals as localized parameters
            }

            return res;
        }


        private static readonly Dictionary<string, object> clientCache = new Dictionary<string, object>();
        private static int id = 0;


        public static T Client<T>(Uri url)
            where T : class
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));
    
            Type t = typeof(T);

            if (!t.IsInterface)
                #pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Generic parameter type must be Interface - not class");
                #pragma warning restore CA1303 // Do not pass literals as localized parameters

            var cacheKey = typeof(T).Name + url;
            lock (clientCache)
            {
                if (clientCache.ContainsKey(cacheKey))
                    return (T)clientCache[cacheKey];
            }

            Utils.Init();

            string typeName = Utils.StripInterfaceName(t.Name) + "HttpRpcImpl" + id;
            id++;

            if (Utils.ModuleBuilder == null)
                #pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Utils.ModuleBuilder can not be null");
                #pragma warning restore CA1303 // Do not pass literals as localized parameters

            var tb = Utils.ModuleBuilder.DefineType(typeName, TypeAttributes.Public);
            tb.AddInterfaceImplementation(typeof(T));

            var aHttpBasePath = t.GetCustomAttribute<HTTPRPCInterfaceAttribute>();
            var iBasePath = "";
            var addInterfaceNameToPath = false;
            if (aHttpBasePath != null)
            {
                iBasePath = aHttpBasePath.BasePath;
                addInterfaceNameToPath=aHttpBasePath.AddInterfaceNameToPath;
            }

            var lUrl = url.AbsoluteUri;
            if (!lUrl.StartsWith("http://",StringComparison.Ordinal) && 
                !lUrl.StartsWith("https://", StringComparison.Ordinal))
            {
                if (lUrl.StartsWith(":", StringComparison.Ordinal))
                    lUrl = "http://" + Environment.MachineName + lUrl;
                if (!lUrl.StartsWith("http://", StringComparison.Ordinal))
                    lUrl = "http://" + lUrl;
                lUrl = lUrl.Trim('/');
                if (!string.IsNullOrEmpty(iBasePath?.Trim()))
                    lUrl += "/" + iBasePath.Trim('/');
            }

            var methods = t.GetMethods();
            foreach (var m in methods)
            {
                var aHTTP = m.GetCustomAttribute<HTTPHandlerAttribute>();
                var lHttpMethod = HttpMethods.Post.ToString().ToUpperInvariant();
                var lPath = "";

                if (aHTTP != null)
                {
                    lHttpMethod = aHTTP.Method.ToString().ToUpperInvariant();
                    lPath = aHTTP.Path;
                    if (!string.IsNullOrEmpty(lPath?.Trim()))
                        lUrl += "/" + lPath.Trim('/');
                }

                var paramsHolderType = Utils.GetMethodRequestType(t, m.Name);

                #pragma warning disable CA1308 // Normalize strings to uppercase
                var fullMethodName = m.Name.ToLowerInvariant();
                #pragma warning restore CA1308 // Normalize strings to uppercase

                if (addInterfaceNameToPath)
                {
                    string iName = Utils.StripInterfaceName(t.Name);
                    if (iName.ToUpperInvariant().Trim() != m.Name.ToUpperInvariant().Trim())
                        fullMethodName = (iName + "/" + m.Name).ToUpperInvariant();
                }

                MethodBuilder mtb = tb.DefineMethod(m.Name, MethodAttributes.Public | MethodAttributes.Virtual, m.ReturnType, Utils.GetMethodParameterTypes(m));
                ILGenerator mtbIL = mtb.GetILGenerator();

                var con = paramsHolderType.GetConstructor(Array.Empty<Type>());
                var getTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle");
                if (con != null)
                {
                    mtbIL.Emit(OpCodes.Ldstr, fullMethodName);
                    mtbIL.Emit(OpCodes.Ldstr, lUrl);
                    mtbIL.Emit(OpCodes.Ldstr, lHttpMethod);
                    mtbIL.Emit(OpCodes.Newobj, con);
                }
                int i = 1;
 
                foreach (var p in m.GetParameters())
                {
                    mtbIL.Emit(OpCodes.Dup);
                    mtbIL.Emit(OpCodes.Ldarg,i);
                    var mset = paramsHolderType.GetMethod("set_"+p.Name); 
                    if(mset!=null)
                        mtbIL.Emit(OpCodes.Callvirt, mset);
                    i++;
                }

                if (getTypeFromHandle != null)
                {
                    mtbIL.Emit(OpCodes.Ldtoken, paramsHolderType);
                    mtbIL.Emit(OpCodes.Call, getTypeFromHandle);
                    mtbIL.Emit(OpCodes.Ldc_I4, (int)(aHTTP != null ? aHTTP.ParametersIn : ParamsPlacedIn.URLPath));
                    mtbIL.Emit(OpCodes.Ldtoken, m.ReturnType);
                    mtbIL.Emit(OpCodes.Call, getTypeFromHandle);
                }
                var method = typeof(HTTPRPCClient).GetMethod("Call");
                if(method !=null)
                    mtbIL.EmitCall(OpCodes.Call, method, new Type[] { typeof(string), typeof(int), typeof(object), typeof(Type), typeof(Type) });

                #region Manage different types of return value
                if (m.ReturnType != typeof(void))
                {
                    if (m.ReturnType.IsClass)
                        mtbIL.Emit(OpCodes.Castclass, m.ReturnType);
                    else if (m.ReturnType == typeof(byte))
                    {
                        var mCovertToByte = typeof(Convert).GetMethod("ToByte", new Type[] { typeof(object) });
                        if(mCovertToByte != null)  
                            mtbIL.Emit(OpCodes.Call, mCovertToByte);
                    }
                    else if (m.ReturnType == typeof(short))
                    {
                        var mCovertToInt16 = typeof(Convert).GetMethod("ToInt16", new Type[] { typeof(object) });
                        if(mCovertToInt16 !=null)
                            mtbIL.Emit(OpCodes.Call, mCovertToInt16);
                    }
                    else if (m.ReturnType == typeof(ushort))
                    {
                        var mCovertToUInt16 = typeof(Convert).GetMethod("ToUInt16", new Type[] { typeof(object) });
                        if(mCovertToUInt16 != null)
                            mtbIL.Emit(OpCodes.Call, mCovertToUInt16);
                    }
                    else if (m.ReturnType == typeof(int))
                    {
                        var mCovertToInt32 = typeof(Convert).GetMethod("ToInt32", new Type[] { typeof(object) });
                        if(mCovertToInt32 != null)
                        mtbIL.Emit(OpCodes.Call, mCovertToInt32);
                    }
                    else if (m.ReturnType == typeof(uint))
                    {
                        var mCovertToUInt32 = typeof(Convert).GetMethod("ToUInt32", new Type[] { typeof(object) });
                        if(mCovertToUInt32 != null)
                            mtbIL.Emit(OpCodes.Call, mCovertToUInt32);
                    }
                    else if (m.ReturnType == typeof(long))
                    {
                        var mCovertToInt64 = typeof(Convert).GetMethod("ToInt64", new Type[] { typeof(object) });
                        if(mCovertToInt64 !=null)    
                            mtbIL.Emit(OpCodes.Call, mCovertToInt64);
                    }
                    else if (m.ReturnType == typeof(ulong))
                    {
                        var mCovertToUInt64 = typeof(Convert).GetMethod("ToUInt64", new Type[] { typeof(object) });
                        if(mCovertToUInt64 !=null)
                            mtbIL.Emit(OpCodes.Call, mCovertToUInt64);
                    }
                    else if (m.ReturnType == typeof(bool))
                    {
                        var mCovertToBoolean = typeof(Convert).GetMethod("ToBoolean", new Type[] { typeof(object) });
                        if(mCovertToBoolean != null)
                            mtbIL.Emit(OpCodes.Call, mCovertToBoolean);
                    }
                    else if (m.ReturnType == typeof(float))
                    {
                        var mCovertToSingle = typeof(Convert).GetMethod("ToSingle", new Type[] { typeof(object) });
                        if(mCovertToSingle != null)
                            mtbIL.Emit(OpCodes.Call, mCovertToSingle);
                    }
                    else if (m.ReturnType == typeof(double))
                    {
                        var mCovertToDouble = typeof(Convert).GetMethod("ToDouble", new Type[] { typeof(object) });
                        if(mCovertToDouble != null)
                            mtbIL.Emit(OpCodes.Call, mCovertToDouble);
                    }
                    else if (m.ReturnType == typeof(DateTime))
                    {
                        var mCovertToDateTime = typeof(Convert).GetMethod("ToDateTime", new Type[] { typeof(object) });
                        if(mCovertToDateTime !=null)
                            mtbIL.Emit(OpCodes.Call, mCovertToDateTime);
                    }
                }
                else
                    mtbIL.Emit(OpCodes.Pop); // Remove function result from stack
                #endregion

                mtbIL.Emit(OpCodes.Ret);
                tb.DefineMethodOverride(mtb, m);
            }

            var res = tb.CreateType();
            T? result = null;
            if (res != null)
            {
                result = (T?)Activator.CreateInstance(res);
                if (result != null)
                {
                    lock (clientCache)
                    {
                        clientCache.Add(cacheKey, result);
                    }
                }
            }

            if (result == null)
                #pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Can't create HttpApi client");
                #pragma warning restore CA1303 // Do not pass literals as localized parameters

            return result;
        }

    }
}
