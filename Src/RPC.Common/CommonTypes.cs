using System;

namespace commanet.Http.RPC
{
    [Flags]
    public enum HttpMethods { Get, Post, Delete, Put }

    public static class Constants
    {
        public const string HEADERCONTINUETOKEN = "x-continuetoken";
        public const string BACKSLASHJOKER = "-!BSH!-";
    }

    public class RPCResponse
    {
        public object Res { get; set; } = "";
        public string ServerError { get; set; } = "";
    }

    public class RPCBinaryResult
    {
        #pragma warning disable CA1051 // Do not declare visible instance fields
        public byte[]? Data;
        public string? ContinueToken;
        #pragma warning restore CA1051 // Do not declare visible instance fields
    }

}