using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using AzureServiceBusDemo.Core;

namespace AzureServiceBusDemo.ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();


            await serviceProvider.GetService<App>().Run();
        }
    }

    public class App
    {
        private readonly IMessageBusFactory _messageBusFactory;

        public App(IMessageBusFactory messageBusFactory)
        {
            this._messageBusFactory = messageBusFactory;
        }

        public async Task Run()
        {
            var client = this._messageBusFactory.GetClient("", "");

            await client.PublishMessageAsync(new
            {
                FirstName = "Bob",
                LastName = "Smith"
            }, "", "");
        }
    }
}
