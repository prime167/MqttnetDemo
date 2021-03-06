﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Common;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Diagnostics;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace MqttNetClient
{
    public partial class FrmMqttClient : Form
    {
        private string _username = "admin";
        private string _password = "public";

        private IMqttClient _mqttClient;

        private Action<string> _updateListBoxAction;

        private readonly List<IManagedMqttClient> _managedMqttClients = new();

        public FrmMqttClient()
        {
            InitializeComponent();
            listBox1.DoubleBuffering(true);

            var logger = new MqttNetLogger();
            logger.LogMessagePublished += (o, args) =>
            {
                var s = new StringBuilder();
                s.Append($"{args.LogMessage.Timestamp} ");
                s.Append($"{args.LogMessage.Level} ");
                s.Append($"{args.LogMessage.Source} ");
                s.Append($"{args.LogMessage.ThreadId} ");
                s.Append($"{args.LogMessage.Message} ");
                s.Append($"{args.LogMessage.Exception}");
                s.Append($"{args.LogMessage.LogId} ");
            };
        }

        private void FrmMqttClient_Load(object sender, EventArgs e)
        {
            foreach (object value in Enum.GetValues(typeof(MqttQualityOfServiceLevel)))
            {
                CmbPubMqttQuality.Items.Add((int)value);
                CmbSubMqttQuality.Items.Add((int)value);
            }

            CmbPubMqttQuality.SelectedItem = 0;
            CmbSubMqttQuality.SelectedIndex = 0;

            _updateListBoxAction = (s) =>
            {
                listBox1.BeginUpdate();
                listBox1.Items.Add(s);
                if (listBox1.Items.Count > 100)
                {
                    listBox1.Items.RemoveAt(0);
                }

                listBox1.TopIndex = listBox1.Items.Count - 1;
                listBox1.EndUpdate();
            };
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            MqttClient();
        }

        private async void BtnDisConnect_Click(object sender, EventArgs e)
        {
            if (_mqttClient is not null && _mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
                _mqttClient.Dispose();
                _mqttClient = null;
            }
        }

        private async void BtnSubscribe_Click(object sender, EventArgs e)
        {
            try
            {
                await _mqttClient.SubscribeAsync(
                    new MqttTopicFilter
                    {
                        Topic = txbSubscribe.Text,
                        QualityOfServiceLevel = (MqttQualityOfServiceLevel)Enum.Parse(typeof(MqttQualityOfServiceLevel), CmbSubMqttQuality.Text)
                    });
            }
            catch (Exception)
            {
                throw;
            }
        }

        private async void BtnSend_Click(object sender, EventArgs e)
        {
            try
            {
                var msg = new MqttApplicationMessage
                {
                    Topic = TxbTopic.Text,
                    Payload = Encoding.UTF8.GetBytes(TxbPayload.Text),
                    QualityOfServiceLevel =
                        (MqttQualityOfServiceLevel)
                            Enum.Parse(typeof(MqttQualityOfServiceLevel), CmbPubMqttQuality.Text),
                    Retain = false
                };

                if (_mqttClient is not null)
                {
                    await _mqttClient.PublishAsync(msg);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void BtnMultiConnect_Click(object sender, EventArgs e)
        {
            MqttMultiClient(Convert.ToInt32(TxbConnectCount.Text));
        }

        private async void BtnMultiDisConnect_Click(object sender, EventArgs e)
        {
            foreach (IManagedMqttClient client in _managedMqttClients)
            {
                await client.StopAsync();
                client.Dispose();
                Thread.Sleep(100);
            }
        }

        private async void MqttClient()
        {
            try
            {
                var options = new MqttClientOptions
                {
                    ClientId = Guid.NewGuid().ToString("D"), 
                    ProtocolVersion = MqttProtocolVersion.V500
                };

                options.ChannelOptions = new MqttClientTcpOptions
                {
                    Server = TxbServer.Text,
                    Port = Convert.ToInt32(TxbPort.Text)
                };

                options.Credentials = new MqttClientCredentials
                {
                    Username = _username,
                    Password = Encoding.UTF8.GetBytes(_password)
                };

                options.CleanSession = true;
                options.KeepAlivePeriod = TimeSpan.FromSeconds(100.5);

                if (null != _mqttClient)
                {
                    await _mqttClient.DisconnectAsync();
                    _mqttClient = null;
                }

                _mqttClient = new MqttFactory().CreateMqttClient();

                _mqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(e =>
                {
                    listBox1.BeginInvoke(
                        _updateListBoxAction,
                        $"{DateTime.Now} ClientID:{e.ClientId} | TOPIC:{e.ApplicationMessage.Topic} | Payload:{Encoding.UTF8.GetString(e.ApplicationMessage.Payload)} | QoS:{e.ApplicationMessage.QualityOfServiceLevel} | Retain:{e.ApplicationMessage.Retain}"
                        );
                });

                _mqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate(e =>
                {
                    listBox1.BeginInvoke(_updateListBoxAction,
                        $"{DateTime.Now} Client is Connected:  IsSessionPresent:{e.AuthenticateResult}");
                });

                _mqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(e =>
                {
                    listBox1.BeginInvoke(_updateListBoxAction,
                        $"{DateTime.Now} Client is DisConnected ClientWasConnected:{e.ClientWasConnected}");
                });

                await _mqttClient.ConnectAsync(options);
            }
            catch (Exception)
            {
                throw;
            }
        }

        private async void MqttMultiClient(int clientsCount)
        {
            for (int i = 0; i < clientsCount; i++)
            {
                var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(new MqttClientOptionsBuilder()
                    .WithClientId(Guid.NewGuid().ToString().Substring(0, 13))
                    .WithTcpServer(TxbServer.Text, Convert.ToInt32(TxbPort.Text))
                    .WithCredentials(_username, _password)
                    .Build()
                )
                .Build();

                IManagedMqttClient c = new MqttFactory().CreateManagedMqttClient();
                await c.SubscribeAsync(
                    new MqttTopicFilterBuilder().WithTopic(txbSubscribe.Text)
                        .WithQualityOfServiceLevel(
                            (MqttQualityOfServiceLevel)
                                Enum.Parse(typeof(MqttQualityOfServiceLevel), CmbSubMqttQuality.Text)).Build());

                await c.StartAsync(options);

                _managedMqttClients.Add(c);

                Thread.Sleep(200);
            }
        }
    }
}
