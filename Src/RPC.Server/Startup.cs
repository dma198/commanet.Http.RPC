using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace commanet.Http.RPC
{
    public partial class HTTPRPCServer
    {
        #pragma warning disable CA1812
        private class Startup : IDisposable
        {

            #region Cache Management
            private const int CACHE_CAPACITY = 1000;
            private const int CACHE_CLEANUP_MS = 60000;

            #pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
            // CryptoService is used for hash only does not need use complicate algorithm 
            private static readonly SHA1 sha = new SHA1CryptoServiceProvider();
            #pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
            private class CacheItem
            {
                public DateTime TimeStamp;
                public int ExpireMs;
                public byte[]? Data;
            }

            private readonly ConcurrentDictionary<string, CacheItem> cache = new ConcurrentDictionary<string, CacheItem>();
            private byte[]? GetFromCache(string key)
            {
                byte[]? res = null;
                if (cache.TryGetValue(key, out CacheItem? it))
                {
                    if (DateTime.Now.Subtract(it.TimeStamp).TotalMilliseconds > it.ExpireMs)
                        cache.TryRemove(key, out _);
                    else res = it.Data;
                }
                CacheCleanup();
                return res;
            }

            private void CacheRemoveOldest()
            {
                CacheItem? oldest = null;
                string? oldestkey = null;
                foreach (var it in cache)
                {
                    if (oldest == null)
                    {
                        oldest = it.Value;
                        oldestkey = it.Key;
                    }
                    else if (oldest.TimeStamp > it.Value.TimeStamp)
                    {
                        oldest = it.Value;
                        oldestkey = it.Key;
                    }
                }
                if (oldest != null && oldestkey != null)
                    cache.TryRemove(oldestkey, out _);
            }

            private DateTime lastCacheCleanup = DateTime.Now;
            private readonly Mutex mxCacheCleanup = new Mutex();
            private void CacheCleanup()
            {
                var now = DateTime.Now;
                if (now.Subtract(lastCacheCleanup).TotalMilliseconds > CACHE_CLEANUP_MS)
                {
                    if (mxCacheCleanup.WaitOne(100))
                    {
                        try
                        {
                            // Double check if cache already cleaned by another thread
                            if (now.Subtract(lastCacheCleanup).TotalMilliseconds > CACHE_CLEANUP_MS)
                            {
                                while (cache.Count > CACHE_CAPACITY)
                                    CacheRemoveOldest();
                                foreach (var it in cache)
                                {
                                    if (now.Subtract(it.Value.TimeStamp).TotalMilliseconds > it.Value.ExpireMs)
                                        cache.TryRemove(it.Key, out _);
                                }
                                lastCacheCleanup = DateTime.Now;
                            }
                        }
                        finally
                        {
                            mxCacheCleanup.ReleaseMutex();
                        }
                    }
                }
            }
            #endregion

            public void Configure(IApplicationBuilder app, IHostingEnvironment _) {


                app.Run(async ctx =>
                {
                    string lpath = ctx.Request.Path.ToString();//.ToUpperInvariant();
             
                    
                    var handler = handlers.Find((h) =>
                    {
                        if (h == null || h.path == null || 
                            h.MethodRequestType == null) return false; 
                        var rxMatch = new Regex(
                             h.path.Replace("/","\\/",StringComparison.Ordinal) +
                             @"(?=(\?|/|(\s*$)))",RegexOptions.IgnoreCase);                      
                        var Match = rxMatch.Match(lpath).Success;
                        //For GET check if parameters passed in url matching (by count) with method argument
                        if (Match && h.httpMethod == HttpMethods.Get)
                        {
                            int argcnt = h.MethodRequestType.GetFields().Length;
                            string argurl = lpath.Substring(h.path.Length).TrimStart(' ', '/');
                            var ars = rxSplitArgs.Split(argurl);
                        }
                        return Match && ctx.Request.Method == h.httpMethod.ToString()
                                                                 .ToUpperInvariant();
                    });
                    if (handler == null)
                    {
                        ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        ctx.Response.ContentType = "text/html";
                        await ctx.Response.WriteAsync("Not found server side handling method for requested path: " + lpath)
                                .ConfigureAwait(false);
                        return;
                    }

                    object[]? args = null;
                    string? hash = null;
                    if (handler!=null && handler.path !=null &&
                        handler.MethodRequestType !=null &&
                        handler.httpMethod == HttpMethods.Get)
                    {
                        if (handler.CacheMilliseconds > 0)
                        {
                            hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(ctx.Request.Path + ctx.Request.QueryString)));
                            var cacheddata = GetFromCache(hash);
                            if (cacheddata != null)
                                await ctx.Response.WriteAsync(Encoding.UTF8.GetString(cacheddata))
                                      .ConfigureAwait(false);
                        }


                        var qargs = new List<KeyValuePair<string?, string?>>();
                        if (ctx.Request.Query.Count == 0 && handler.path.Length<lpath.Length)
                        {
                            var tmp = rxSplitArgs.Split(lpath.Substring(handler.path.Length + 1));
                            foreach (var v in tmp) 
                                qargs.Add(new KeyValuePair<string?, string?>(null, v));
                        }
                        else
                        {
                            foreach (var q in ctx.Request.Query)
                            {
                                if(q.Value=="")
                                    qargs.Add(new KeyValuePair<string?, string?>(null, q.Key));
                                else 
                                    qargs.Add(new KeyValuePair<string?, string?>(q.Key, q.Value));
                            }
                        }

                        List<object> largs = new List<object>();
                        var prps = handler.MethodRequestType.GetProperties();
                        for (int i = 0; i < prps.Length; i++)
                        {
                            string? v = null;
                            if (qargs.Exists((a) => a.Key != null && a.Key.Trim().ToUpperInvariant() == prps[i].Name.Trim().ToUpperInvariant()))
                            {
                                v = qargs.Find((a) => a.Key != null && a.Key.Trim().ToUpperInvariant() == prps[i].Name.Trim().ToUpperInvariant()).Value;
                            }
                            else
                            {
                                if (i < qargs.Count)
                                    v = qargs[i].Value;
                            }
                            if (v == null) continue;
                            v = Uri.UnescapeDataString(v);
                            if (prps[i].PropertyType == typeof(bool))
                            {
                                #pragma warning disable CA1308 // Normalize strings to uppercase
                                v = v.ToLowerInvariant();
                                #pragma warning restore CA1308 // Normalize strings to uppercase
                            };
                            if (prps[i].PropertyType == typeof(string))
                            {
                                v = v.Replace(Constants.BACKSLASHJOKER, "\\",StringComparison.Ordinal);
                                largs.Add(v);
                            }
                            else
                            {
                                var options = new JsonSerializerOptions();
                                var obj = JsonSerializer.Deserialize(v, prps[i].PropertyType,options);
                                largs.Add(obj);
                            }
                        }
                        args = largs.ToArray();
                    }
                    else
                    {

                        List<byte> lbuf = new List<byte>();
                        int cnt = 1;
                        int totalcnt = 0;
                        
                        while(cnt > 0)
                        {
                            byte[] buf = new byte[1024];
                            
                            cnt = await ctx.Request.Body.ReadAsync(buf, 0, buf.Length)
                                        .ConfigureAwait(false);
                            //cnt = ctx.Request.Body.Read(buf, 0, buf.Length);
                            if (cnt > 0)
                            {
                                lbuf.AddRange(buf);
                                totalcnt += cnt;
                            }
                        } //1334272,1333347
                        string json = Encoding.UTF8.GetString(lbuf.ToArray(), 0, totalcnt);
                        if (handler!=null && handler.CacheMilliseconds > 0)
                        {
                            hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(ctx.Request.Path + json)));
                            var cacheddata = GetFromCache(hash);
                            if (cacheddata != null)
                                await ctx.Response.WriteAsync(Encoding.UTF8.GetString(cacheddata))
                                         .ConfigureAwait(false);
                        }

                        if (handler != null && handler.path !=null)
                        {
                            string[] tmp = handler.path.Split('/');
                            var IntName = tmp[^2];
                            var ProcName = tmp[^1];

                            try
                            {
                                var a = JsonSerializer.Deserialize(json, handler.MethodRequestType);
                                if (handler != null && handler.MethodRequestType != null)
                                {
                                    var prps = handler.MethodRequestType.GetProperties();
                                    args = new object[prps.Length];
                                    int ai = 0;
                                    for (int i = 0; i < prps.Length; i++)
                                    {
                                        var arg = prps[i].GetValue(a);
                                        if (prps[i] == null || arg == null) continue;
                                        args[ai] = arg;
                                        ai++;
                                    }
                                }
                            }
                            #pragma warning disable CA1031 // Do not catch general exception types
                            catch (Exception ex)
                            {
                                Exception lex = ex;
                                while (lex.InnerException != null) lex = lex.InnerException;
                                ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                                ctx.Response.ContentType = "application/json";
                                var response = new RPCResponse
                                {
                                    ServerError = "Cant't accept passed parameters. Error during its parsing:  " + lex.Message
                                };
                                var res = JsonSerializer.Serialize(response);
                                await ctx.Response.WriteAsync(res).ConfigureAwait(false);
                            }
                            #pragma warning restore CA1031 // Do not catch general exception types
                        }
                    }
                    if (handler !=null  && handler.m != null && !handler.BinaryResult)
                    {
                        try
                        {
                            var res = handler.m.Invoke(handler.obj, args);
                            byte[] data = Array.Empty<byte>();

                            ctx.Response.ContentType = "application/json";
                            var resSerialized = JsonSerializer.Serialize(res);
                            await ctx.Response.WriteAsync(resSerialized)
                                    .ConfigureAwait(false);
                            data = Encoding.UTF8.GetBytes(resSerialized);

                            if (hash !=null && handler.CacheMilliseconds > 0)
                            {
                                cache.TryAdd(hash, new CacheItem()
                                {
                                    TimeStamp = DateTime.Now,
                                    ExpireMs = handler.CacheMilliseconds,
                                    Data = data
                                });
                            }
                        }
                        #pragma warning disable CA1031 // Do not catch general exception types
                        catch (Exception ex)
                        {
                            Exception lex = ex;
                            while (lex.InnerException != null) lex = lex.InnerException;
                            ctx.Response.ContentType = "text/html";
                            ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            await ctx.Response.WriteAsync(lex.Message)
                                     .ConfigureAwait(false);
                        }
                        #pragma warning restore CA1031 // Do not catch general exception types

                    }
                    else if (handler !=null && handler.m != null)// Binary result handling
                    {
                        var res = new RPCBinaryResult();
                        try
                        {
                            res = (RPCBinaryResult?)handler.m.Invoke(handler.obj, args);
                        }
                        #pragma warning disable CA1031 // Do not catch general exception types
                        catch (Exception ex)
                        {
                            Exception lex = ex;
                            while (lex.InnerException != null) lex = lex.InnerException;
                            ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            await ctx.Response.WriteAsync(lex.Message)
                                    .ConfigureAwait(false);
                        }
                        #pragma warning restore CA1031 // Do not catch general exception types

                        if (res !=null && res.ContinueToken != null && !string.IsNullOrEmpty(res.ContinueToken))
                        {
                            string[] customHeader = new string[1] { res.ContinueToken };
                            ctx.Response.Headers.Add(Constants.HEADERCONTINUETOKEN, customHeader);
                        }
                        ctx.Response.ContentType = "application/octet-stream";
                        ctx.Response.ContentLength = res == null || res.Data ==null ? 0 : res.Data.Length;

                        var data = res == null || res.Data == null ? Array.Empty<byte>() : res.Data;
                        await ctx.Response.WriteAsync(Encoding.UTF8.GetString(data))
                                .ConfigureAwait(false);
                    }

                });
            }

            private bool isDisposed;

            public Startup()
            {
            }

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
                    mxCacheCleanup?.Dispose();
                }

                isDisposed = true;
            }


            #region Private Methods

            #endregion

        }
        #pragma warning restore CA1812
    }
}
