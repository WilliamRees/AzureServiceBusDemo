using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AzureServiceBusDemo.Core
{
    public interface IMessageBus
    {
        Task PublishMessageAsync<T>(T message);
    }

    public interface IMessageBusFactory
    {
        IMessageBus GetClient(string connectionString, string sender);
    }

    internal class AzureServiceBus : IMessageBus
    {
        private readonly ServiceBusSender _serviceBusSender;

        internal AzureServiceBus(ServiceBusSender serviceBusSender)
        {
            this._serviceBusSender = serviceBusSender;
        }

        public async Task PublishMessageAsync<T>(T message)
        {
            var jsonString = JsonSerializer.Serialize(message);

            var serviceBusMessage = new ServiceBusMessage(Encoding.UTF8.GetBytes(jsonString));

            await this._serviceBusSender.SendMessageAsync(serviceBusMessage);
        }

        internal static IMessageBus Create(ServiceBusSender sender)
        {
            return new AzureServiceBus(sender);
        }
    }

    public class AzureServiceBusFactory : IMessageBusFactory
    {
        private readonly object _lockObject = new object();

        private readonly ConcurrentDictionary<string, ServiceBusClient> _clients = new ConcurrentDictionary<string, ServiceBusClient>();

        private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new ConcurrentDictionary<string, ServiceBusSender>();

        public IMessageBus GetClient(string connectionString, string senderName)
        {
            var key = $"{connectionString}-{senderName}";

            if (this._senders.ContainsKey(key) && !this._senders[key].IsClosed)
            {
                return AzureServiceBus.Create(this._senders[key]);
            }

            var client = this.GetServiceBusClient(connectionString);

            lock (this._lockObject)
            {
                if (this._senders.ContainsKey(key) && this._senders[key].IsClosed)
                {
                    if (this._senders[key].IsClosed)
                    {
                        this._senders[key].DisposeAsync().GetAwaiter().GetResult();
                    }

                    return AzureServiceBus.Create(this._senders[key]);
                }

                var sender = client.CreateSender(senderName);

                this._senders[key] = sender;
            }

            return AzureServiceBus.Create(this._senders[key]);
        }


        protected virtual ServiceBusClient GetServiceBusClient(string connectionString)
        {
            var key = $"{connectionString}";

            lock (this._lockObject)
            {
                if (this.ClientDoesntExistOrIsClosed(connectionString))
                {
                    var client = new ServiceBusClient(connectionString, new ServiceBusClientOptions
                    {
                        TransportType = ServiceBusTransportType.AmqpTcp
                    });

                    this._clients[key] = client;
                }

                return this._clients[key];
            }
        }

        private bool ClientDoesntExistOrIsClosed(string connectionString)
        {
            return !this._clients.ContainsKey(connectionString) || this._clients[connectionString].IsClosed;
        }
    }

    public static class DependencyInjectionExtensions 
    { 
        public static IServiceCollection AddAzureServiceBusFactory(this IServiceCollection services)
        {
            services.AddSingleton<IMessageBusFactory, AzureServiceBusFactory>();
            return services;
        }
    }
}
