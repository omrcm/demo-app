using BOA.Common.Types;
using BOA.Types.InternetBanking;
using BOA.Types.InternetBanking.FX;
using BOA.Types.Kernel.General;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using VK.Emar.Application.Services;
using VK.Emar.Extensions.DependencyInjection;
using VK.Emar.Json;
using VK.Fx.DataFeed.Application.Contract;
using VK.Fx.DataFeed.BoaProxy;
using VK.Fx.DataFeed.Domain.Shared;

namespace VK.Fx.DataFeed.Application
{
    public class RabbitService : ApplicationService, IRabbitService
    {

        private IConfiguration _configuration;
        private readonly IJsonSerializer _serializer;
        private IConnection connection;
        private IModel channel;
        public RabbitService(ILazyServiceProvider lazyServiceProvide, IJsonSerializer serializer, IConfiguration configuration) : base(lazyServiceProvide)
        {
            _serializer = serializer;
            _configuration = configuration;

            ConnectFactory();
        }
        public void ConnectFactory()
        {
            var rabbitHostName = _configuration.GetSection("Emar:RabbitMQ:Connections:Default:HostName").Value;
            var rabbitPort = _configuration.GetSection("Emar:RabbitMQ:Connections:Default:Port").Value;
            var rabbitUserName = _configuration.GetSection("Emar:RabbitMQ:Connections:Default:UserName").Value;
            var rabbitPassword = _configuration.GetSection("Emar:RabbitMQ:Connections:Default:Password").Value;
            string conn2 = string.Format("amqp://{0}:{1}@{2}:{3}", rabbitUserName, rabbitPassword, rabbitHostName, rabbitPort);
            Logger.LogInformation("Rabbit connection : " + conn2);
            var factory = new ConnectionFactory() { Uri = new Uri(conn2), RequestedHeartbeat = TimeSpan.FromSeconds(60) };
            try
            {
                this.connection = factory.CreateConnection();
                this.channel = connection.CreateModel();

            }
            catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException ex)
            {
                Logger.LogError("Error: Rabbit Connection1 : " + ex.Message);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error: Rabbit Connection2 : " + ex.Message);
            }
        }
        public string Post(string group, string topic, FXSymbolDto symbolDto)
        {
            try
            {
                channel.ExchangeDeclare(exchange: group, type: ExchangeType.Direct);


                channel.QueueDeclare(queue: topic,
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: true,
                                     arguments: null);

                var symbolData = _serializer.Serialize(symbolDto);
                var body = Encoding.UTF8.GetBytes(symbolData);


                channel.QueueBind(queue: topic, exchange: group, routingKey: topic);


                channel.BasicPublish(exchange: group,
                               routingKey: topic,
                               basicProperties: null,
                               body: body);
                return $"[x] Sent {topic}";
            }
            catch (System.Exception ex)
            {
                Logger.LogError("Error: Rabbit e veri gönderilemed : " + ex.Message);
                return "";
            }

        }

        public string BathcPost(List<FXSymbolDto> symbolDtoList, string group)
        {
            try
            {
                // Bağlantı ve kanalın açık olup olmadığını kontrol et
                if (this.connection == null || this.channel == null)
                    ConnectFactory();
                if (this.connection == null || this.channel == null)
                { return "Rabbit Connection Error"; }
                if ((this.connection != null && this.connection.IsOpen))
                {
                    foreach (var obj in symbolDtoList)
                    {
                        try
                        {
                            using (var localChannel = this.connection.CreateModel())
                            {
                                localChannel.ConfirmSelect();
                                localChannel.ExchangeDeclare(exchange: "exc_" + group, type: ExchangeType.Direct, durable: false);
                                string topicname = string.Format("{0}/{1}_{2}", group, obj.Fec1, obj.Fec2);
                                localChannel.QueueDeclare(queue: topicname, durable: false, exclusive: false, autoDelete: false, arguments: null);
                                localChannel.QueueBind(queue: topicname, exchange: "exc_" + group, routingKey: topicname);

                                var symbolData = _serializer.Serialize(obj);
                                var body = Encoding.UTF8.GetBytes(symbolData);
                                var properties = localChannel.CreateBasicProperties();

                                localChannel.BasicPublish(exchange: "exc_" + group,
                                                          routingKey: topicname,
                                                          basicProperties: properties,
                                                          body: body);

                                localChannel.WaitForConfirmsOrDie();
                                Logger.LogInformation("Rabbit Mesaj Gönderildi." + symbolData);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Error: Rabbit : " + ex.Message);
                        }
                    }
                }

                return "";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
