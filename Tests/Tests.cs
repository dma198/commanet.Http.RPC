using System;
using System.Threading;
using System.Linq;
using Xunit;

using commanet.Http.RPC;

namespace DA.SI.Core.Http.RPC.Server.Tests
{
    public class Tests : IDisposable
    {
        private readonly IHttpAPIGet cli;
        private readonly IHttpAPIPost cliPost;
        private HTTPRPCServer server=null;

        public Tests()
        {
            server = new HTTPRPCServer("localhost", 5010);

            server.Run();

            cli = HTTPRPCClient.Client<IHttpAPIGet>(new Uri("http://localhost:5010/api"));
            cliPost = HTTPRPCClient.Client<IHttpAPIPost>(new Uri("http://localhost:5010/api"));
        }

        public void Dispose()
        {
            server?.Dispose();
        }

        [Fact]
        public void TestServerSideException()
        {
            try
            {
                cli.ServerSideException();
                throw new Exception("Not generated server side exception");
            }
            catch(Exception ex)
            {
                if(!ex.Message.EndsWith("Server Side Test Exception"))
                    throw new Exception("Missing server side exeption description");
            }
        }

        [Fact]
        public void TestCommunicationError()
        {
            try
            {
                var lcli = HTTPRPCClient.Client<IHttpAPIGet>(new Uri("http://fakehostfortest:9999"));
                var r = lcli.IntResult();
                throw new Exception("Not generated exception at communication fault");
            }
            catch (Exception)
            {
            }
        }


        [Fact]
        public void TestResultTypesGet()
        {
            if (cli.IntResult() != -123) throw new Exception("Invalid int result in GET call");
            if (cli.UIntResult() != 123) throw new Exception("Invalid uint result in GET call");
            if (cli.LongResult() != -1234567890) throw new Exception("Invalid long result in GET call");
            if (cli.ULongResult() != 1234567890) throw new Exception("Invalid ulong result in GET call");
            if (cli.DoubleResult() != 123456789.987654321) throw new Exception("Invalid double result in GET call");
            if (cli.FloatResult() != (float)1.234) throw new Exception("Invalid float result in GET call");
            if (!cli.BoolResult()) throw new Exception("Invalid bool result in GET call");
            if (cli.DateTimeResult() != new DateTime(2011, 05, 27, 1, 2, 3)) throw new Exception("Invalid DateTime result in GET call");
            if (cli.ObjectResult().StrField != "Test") throw new Exception("Invalid Object result in GET call");
        }

        [Fact]
        public void TestParameterTypesGet()
        {
            if (cli.IntParameter(-123) != -123) throw new Exception("Invalid int parameter management in GET call");
            if (cli.UIntParameter(123) != 123) throw new Exception("Invalid uint parameter management in GET call");
            if (cli.LongParameter(-123456789) != -123456789) throw new Exception("Invalid long parameter management in GET call");
            if (cli.ULongParameter(123456789) != 123456789) throw new Exception("Invalid ulong parameter management in GET call");
            if (cli.BoolParameter(true) != true) throw new Exception("Invalid bool parameter management in GET call");
            if (cli.DoubleParameter(12345.56789) != 12345.56789) throw new Exception("Invalid double parameter management in GET call");
            if (cli.FloatParameter((float)123.456) != (float)123.456) throw new Exception("Invalid float parameter management in GET call");
            var tm = DateTime.Now;
            if (cli.DateTimeParameter(tm) != tm) throw new Exception("Invalid DateTime parameter management in GET call");
            var obj = cli.ObjectParameter(new Data() { StrField = "Test" });
            if (obj.StrField != "Test") throw new Exception("Invalid Object parameter management in GET call");
        }

        [Fact]
        public void TestParameterTypesQGet()
        {
            if (cli.IntParameterQ(-123) != -123) throw new Exception("Invalid int parameter management in GET call");
            if (cli.UIntParameterQ(123) != 123) throw new Exception("Invalid uint parameter management in GET call");
            if (cli.LongParameterQ(-123456789) != -123456789) throw new Exception("Invalid long parameter management in GET call");
            if (cli.ULongParameterQ(123456789) != 123456789) throw new Exception("Invalid ulong parameter management in GET call");
            if (cli.BoolParameterQ(true) != true) throw new Exception("Invalid bool parameter management in GET call");
            if (cli.DoubleParameterQ(12345.56789) != 12345.56789) throw new Exception("Invalid double parameter management in GET call");
            if (cli.FloatParameterQ((float)123.456) != (float)123.456) throw new Exception("Invalid float parameter management in GET call");
            var tm = DateTime.Now;
            if (cli.DateTimeParameterQ(tm) != tm) throw new Exception("Invalid DateTime parameter management in GET call");
            var obj = cli.ObjectParameterQ(new Data() { StrField = "Test" });
            if (obj.StrField != "Test") throw new Exception("Invalid Object parameter management in GET call");
        }

        [Fact]
        public void TestResultTypesPost()
        {
            if (cliPost.IntResult() != -123) throw new Exception("Invalid int result in GET call");
            if (cliPost.UIntResult() != 123) throw new Exception("Invalid uint result in GET call");
            if (cliPost.LongResult() != -1234567890) throw new Exception("Invalid long result in GET call");
            if (cliPost.ULongResult() != 1234567890) throw new Exception("Invalid ulong result in GET call");
            if (cliPost.DoubleResult() != 123456789.987654321) throw new Exception("Invalid double result in GET call");
            if (cliPost.FloatResult() != (float)1.234) throw new Exception("Invalid float result in GET call");
            if (!cliPost.BoolResult()) throw new Exception("Invalid bool result in GET call");
            if (cliPost.DateTimeResult() != new DateTime(2011, 05, 27, 1, 2, 3)) throw new Exception("Invalid DateTime result in GET call");
            if (cliPost.ObjectResult().StrField != "Test") throw new Exception("Invalid Object result in POST call");
        }

        [Fact]
        public void TestParameterTypesPost()
        {
            if (cliPost.IntParameter(-123) != -123) throw new Exception("Invalid int parameter management in GET call");
            if (cliPost.UIntParameter(123) != 123) throw new Exception("Invalid uint parameter management in GET call");
            if (cliPost.LongParameter(-123456789) != -123456789) throw new Exception("Invalid long parameter management in GET call");
            if (cliPost.ULongParameter(123456789) != 123456789) throw new Exception("Invalid ulong parameter management in GET call");
            if (cliPost.BoolParameter(true) != true) throw new Exception("Invalid bool parameter management in GET call");
            if (cliPost.DoubleParameter(12345.56789) != 12345.56789) throw new Exception("Invalid double parameter management in GET call");
            if (cliPost.FloatParameter((float)123.456) != (float)123.456) throw new Exception("Invalid float parameter management in GET call");
            var tm = DateTime.Now;
            if (cliPost.DateTimeParameter(tm) != tm) throw new Exception("Invalid DateTime parameter management in GET call");
            var obj = cliPost.ObjectParameter(new Data() { StrField = "Test" });
            if (obj.StrField != "Test") throw new Exception("Invalid Object parameter management in POST call");
        }

        [Fact]
        public void TestBinaryResultGet()
        {
            var expects = new byte[1000000];
            byte b = 0;
            for (int i = 0; i < expects.Length; i++)
            {
                expects[i] = b;
                b++;
            }
            var res = cli.Binary();
            if(!expects.SequenceEqual(res)) throw new Exception("Invalid binary result in GET call");
        }

         
        [Fact]
        public void TestBinaryResultPost()
        {
            var expects = new byte[10000];
            byte b = 0;
            for (int i = 0; i < expects.Length; i++)
            {
                expects[i] = b;
                b++;
            }
            var res = cliPost.Binary(expects);
            if (!expects.SequenceEqual(res)) throw new Exception("Invalid binary result in POST call");
        }

    }

    [HTTPRPCInterface]
    public interface IHttpAPIGet
    {
        #region Result type testing methods
        [HTTPHandler(Method = HttpMethods.Get)]
        int IntResult();

        [HTTPHandler(Method = HttpMethods.Get)]
        uint UIntResult();

        [HTTPHandler(Method = HttpMethods.Get)]
        long LongResult();

        [HTTPHandler(Method = HttpMethods.Get)]
        ulong ULongResult();

        [HTTPHandler(Method = HttpMethods.Get)]
        double DoubleResult();

        [HTTPHandler(Method = HttpMethods.Get)]
        float FloatResult();

        [HTTPHandler(Method = HttpMethods.Get)]
        bool BoolResult();

        [HTTPHandler(Method = HttpMethods.Get)]
        DateTime DateTimeResult();

        [HTTPHandler(Method = HttpMethods.Get)]
        Data ObjectResult();
        #endregion

        void ServerSideException();

        #region Parameter Types Testing Methods (Params passed in URL)
        [HTTPHandler(Method = HttpMethods.Get)]
        int IntParameter(int p);
        [HTTPHandler(Method = HttpMethods.Get)]
        uint UIntParameter(uint p);
        [HTTPHandler(Method = HttpMethods.Get)]
        long LongParameter(long p);
        [HTTPHandler(Method = HttpMethods.Get)]
        ulong ULongParameter(ulong p);
        [HTTPHandler(Method = HttpMethods.Get)]
        bool BoolParameter(bool p);
        [HTTPHandler(Method = HttpMethods.Get)]
        double DoubleParameter(double p);
        [HTTPHandler(Method = HttpMethods.Get)]
        float FloatParameter(float p);
        [HTTPHandler(Method = HttpMethods.Get)]
        DateTime DateTimeParameter(DateTime p);
        [HTTPHandler(Method = HttpMethods.Get)]
        Data ObjectParameter(Data p);
        #endregion

        #region Parameter Types Testing Methods (Params passed in QUERY)
        [HTTPHandler(Method = HttpMethods.Get, ParametersIn = ParamsPlacedIn.Query)]
        int IntParameterQ(int p);
        [HTTPHandler(Method = HttpMethods.Get, ParametersIn = ParamsPlacedIn.Query)]
        uint UIntParameterQ(uint p);
        [HTTPHandler(Method = HttpMethods.Get, ParametersIn = ParamsPlacedIn.Query)]
        long LongParameterQ(long p);
        [HTTPHandler(Method = HttpMethods.Get, ParametersIn = ParamsPlacedIn.Query)]
        ulong ULongParameterQ(ulong p);
        [HTTPHandler(Method = HttpMethods.Get, ParametersIn = ParamsPlacedIn.Query)]
        bool BoolParameterQ(bool p);
        [HTTPHandler(Method = HttpMethods.Get, ParametersIn = ParamsPlacedIn.Query)]
        double DoubleParameterQ(double p);
        [HTTPHandler(Method = HttpMethods.Get, ParametersIn = ParamsPlacedIn.Query)]
        float FloatParameterQ(float p);
        [HTTPHandler(Method = HttpMethods.Get, ParametersIn = ParamsPlacedIn.Query)]
        DateTime DateTimeParameterQ(DateTime p);
        [HTTPHandler(Method = HttpMethods.Get, ParametersIn = ParamsPlacedIn.Query)]
        Data ObjectParameterQ(Data p);
        #endregion

        [HTTPHandler(Method = HttpMethods.Get)]
        byte[] Binary();

    }

    [HTTPRPCInterface]
    public interface IHttpAPIPost
    {
        #region Result type testing methods
        [HTTPHandler(Method = HttpMethods.Post)]
        int IntResult();

        [HTTPHandler(Method = HttpMethods.Post)]
        uint UIntResult();

        [HTTPHandler(Method = HttpMethods.Post)]
        long LongResult();

        [HTTPHandler(Method = HttpMethods.Post)]
        ulong ULongResult();

        [HTTPHandler(Method = HttpMethods.Post)]
        double DoubleResult();

        [HTTPHandler(Method = HttpMethods.Post)]
        float FloatResult();

        [HTTPHandler(Method = HttpMethods.Post)]
        bool BoolResult();

        [HTTPHandler(Method = HttpMethods.Post)]
        DateTime DateTimeResult();

        [HTTPHandler(Method = HttpMethods.Post)]
        Data ObjectResult();
        #endregion

        void ServerSideException();

        #region Parameter Types Testing Methods (Params passed in URL)
        [HTTPHandler(Method = HttpMethods.Post)]
        int IntParameter(int p);
        [HTTPHandler(Method = HttpMethods.Post)]
        uint UIntParameter(uint p);
        [HTTPHandler(Method = HttpMethods.Post)]
        long LongParameter(long p);
        [HTTPHandler(Method = HttpMethods.Post)]
        ulong ULongParameter(ulong p);
        [HTTPHandler(Method = HttpMethods.Post)]
        bool BoolParameter(bool p);
        [HTTPHandler(Method = HttpMethods.Post)]
        double DoubleParameter(double p);
        [HTTPHandler(Method = HttpMethods.Post)]
        float FloatParameter(float p);
        [HTTPHandler(Method = HttpMethods.Post)]
        DateTime DateTimeParameter(DateTime p);
        [HTTPHandler(Method = HttpMethods.Post)]
        Data ObjectParameter(Data p);
        #endregion

        #region Parameter Types Testing Methods (Params passed in QUERY)
        [HTTPHandler(Method = HttpMethods.Post)]
        int IntParameterQ(int p);
        [HTTPHandler(Method = HttpMethods.Post)]
        uint UIntParameterQ(uint p);
        [HTTPHandler(Method = HttpMethods.Post)]
        long LongParameterQ(long p);
        [HTTPHandler(Method = HttpMethods.Post)]
        ulong ULongParameterQ(ulong p);
        [HTTPHandler(Method = HttpMethods.Post)]
        bool BoolParameterQ(bool p);
        [HTTPHandler(Method = HttpMethods.Post)]
        double DoubleParameterQ(double p);
        [HTTPHandler(Method = HttpMethods.Post)]
        float FloatParameterQ(float p);
        [HTTPHandler(Method = HttpMethods.Post)]
        DateTime DateTimeParameterQ(DateTime p);
        [HTTPHandler(Method = HttpMethods.Post)]
        Data ObjectParameterQ(Data p);
        #endregion

        byte[] Binary(byte[] data);
    }

    public class HttpServer : IHttpAPIGet
    {
        #region Result type testing methods
        public int IntResult() { return -123; }

        public uint UIntResult() { return 123; }

        public long LongResult() { return -1234567890; }

        public ulong ULongResult() { return 1234567890; }

        public double DoubleResult() { return 123456789.987654321; }
        public float FloatResult() { return (float)1.234; }

        public bool BoolResult() { return true; }

        public DateTime DateTimeResult()
        {
            return new DateTime(2011,05,27,1,2,3);
        }

        public Data ObjectResult()
        {
            return new Data {
                StrField = "Test"
            };
        }
        #endregion

        public void ServerSideException()
        {
            throw new Exception("Server Side Test Exception");
        }
        #region Parameter Types Testing Methods (Parameters Passed in URL)
        public int IntParameter(int p) { return p; }
        public uint UIntParameter(uint p) { return p; }
        public long LongParameter(long p) { return p; }
        public ulong ULongParameter(ulong p) { return p; }
        public bool BoolParameter(bool p) { return p; }
        public double DoubleParameter(double p) { return p; }
        public float FloatParameter(float p) { return p; }
        public DateTime DateTimeParameter(DateTime p) { return p; }
        public Data ObjectParameter(Data p) { return p; }
        #endregion

        #region Parameter Types Testing Methods (Parameters Passed in QUERY)
        public int IntParameterQ(int p) { return p; }
        public uint UIntParameterQ(uint p) { return p; }
        public long LongParameterQ(long p) { return p; }
        public ulong ULongParameterQ(ulong p) { return p; }
        public bool BoolParameterQ(bool p) { return p; }
        public double DoubleParameterQ(double p) { return p; }
        public float FloatParameterQ(float p) { return p; }
        public DateTime DateTimeParameterQ(DateTime p) { return p; }
        public Data ObjectParameterQ(Data p) { return p; }
        #endregion

        public byte[] Binary() {
            var data=new byte[1000000];
            byte b = 0;
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = b;
                b++;
            }
            return data;
        }
    }

    public class HttpServerPost : IHttpAPIPost
    {
        #region Result type testing methods
        public int IntResult() { return -123; }

        public uint UIntResult() { return 123; }

        public long LongResult() { return -1234567890; }

        public ulong ULongResult() { return 1234567890; }

        public double DoubleResult() { return 123456789.987654321; }
        public float FloatResult() { return (float)1.234; }

        public bool BoolResult() { return true; }

        public DateTime DateTimeResult()
        {
            return new DateTime(2011, 05, 27, 1, 2, 3);
        }

        public Data ObjectResult()
        {
            return new Data
            {
                StrField = "Test"
            };
        }
        #endregion

        public void ServerSideException()
        {
            throw new Exception("Server Side Test Exception");
        }
        #region Parameter Types Testing Methods (Parameters Passed in URL)
        public int IntParameter(int p) { return p; }
        public uint UIntParameter(uint p) { return p; }
        public long LongParameter(long p) { return p; }
        public ulong ULongParameter(ulong p) { return p; }
        public bool BoolParameter(bool p) { return p; }
        public double DoubleParameter(double p) { return p; }
        public float FloatParameter(float p) { return p; }
        public DateTime DateTimeParameter(DateTime p) { return p; }
        public Data ObjectParameter(Data p) { return p; }
        #endregion

        #region Parameter Types Testing Methods (Parameters Passed in QUERY)
        public int IntParameterQ(int p) { return p; }
        public uint UIntParameterQ(uint p) { return p; }
        public long LongParameterQ(long p) { return p; }
        public ulong ULongParameterQ(ulong p) { return p; }
        public bool BoolParameterQ(bool p) { return p; }
        public double DoubleParameterQ(double p) { return p; }
        public float FloatParameterQ(float p) { return p; }
        public DateTime DateTimeParameterQ(DateTime p) { return p; }
        public Data ObjectParameterQ(Data p) { return p; }
        #endregion

        public byte[] Binary(byte[] data)
        {
            return data;
        }


    }


    public class Data
    {
        public string StrField { get; set; }
    }

}
