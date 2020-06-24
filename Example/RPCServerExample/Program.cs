using System.Threading.Tasks;
using commanet;

namespace RPCServerExample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await new Application<Manager>(args)
            .RunAsync()
            .ConfigureAwait(false);
        }
    }
}
