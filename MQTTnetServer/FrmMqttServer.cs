﻿using System;
using System.Net;
using System.Text;
using System.Windows.Forms;
using Common;
using MQTTnet;
using MQTTnet.Client.Receiving;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace MqttNetServer
{
    public partial class FrmMqttServer : Form
    {
        private IMqttServer _mqttServer;

        private Action<string> _updateListBoxAction;

        public FrmMqttServer()
        {
            InitializeComponent();
            listBox1.DoubleBuffering(true);
        }

        private void FrmMqttServer_Load(object sender, EventArgs e)
        {
            _updateListBoxAction = s =>
            {
                listBox1.BeginUpdate();
                listBox1.Items.Add(s);
                if (listBox1.Items.Count > 1000)
                {
                    listBox1.Items.RemoveAt(0);
                }

                listBox1.TopIndex = listBox1.Items.Count - 1;
                listBox1.EndUpdate();
            };


            listBox1.KeyPress += (o, args) =>
            {
                if (args.KeyChar == 'c' || args.KeyChar == 'C')
                {
                    listBox1.Items.Clear();
                }
            };

            BtnStart.Enabled = true;
            BtnStop.Enabled = false;
            TxbServer.Enabled = true;
            TxbPort.Enabled = true;
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            MqttServer();
            BtnStart.Enabled = false;
            BtnStop.Enabled = true;
            TxbServer.Enabled = false;
            TxbPort.Enabled = false;
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (_mqttServer is not null)
            {
                foreach (var clientSessionStatus in _mqttServer.GetSessionStatusAsync().Result)
                {
                    clientSessionStatus.DeleteAsync();
                }

                _mqttServer.StopAsync();
                _mqttServer = null;
            }

            BtnStart.Enabled = true;
            BtnStop.Enabled = false;
            TxbServer.Enabled = true;
            TxbPort.Enabled = true;
        }

        private async void MqttServer()
        {
            if (_mqttServer is not null)
            {
                return;
            }

            var optionBuilder =
                new MqttServerOptionsBuilder()
                    .WithConnectionBacklog(1000)
                    .WithDefaultEndpointPort(Convert.ToInt32(TxbPort.Text));

            if (!string.IsNullOrEmpty(TxbServer.Text))
            {
                optionBuilder.WithDefaultEndpointBoundIPAddress(IPAddress.Parse(TxbServer.Text));
            }

            optionBuilder.WithConnectionValidator(c =>
            {
                if (c.ClientId.Length < 10)
                {
                    c.ReasonCode = MqttConnectReasonCode.ClientIdentifierNotValid;
                    return;
                }

                if (!c.Username.Equals("admin"))
                {
                    c.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                    return;
                }

                if (!c.Password.Equals("public"))
                {
                    c.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                    return;
                }

                c.ReasonCode = MqttConnectReasonCode.Success;
            });

            _mqttServer = new MqttFactory().CreateMqttServer();
            _mqttServer.ClientConnectedHandler = new MqttServerClientConnectedHandlerDelegate(e =>
             {
                 listBox1.BeginInvoke(_updateListBoxAction, $"{DateTime.Now} Client Connected:ClientId:{e.ClientId}");
                 var s = _mqttServer.GetSessionStatusAsync();
                 lblClientCount.BeginInvoke(new Action(() => { lblClientCount.Text = $@"{DateTime.Now} 连接总数：{s.Result.Count}"; }));
             });

            _mqttServer.ClientDisconnectedHandler = new MqttServerClientDisconnectedHandlerDelegate(e =>
            {
                listBox1.BeginInvoke(_updateListBoxAction, $"{DateTime.Now} Client Disconnected:ClientId:{e.ClientId}");
                var s = _mqttServer.GetSessionStatusAsync();
                lblClientCount.BeginInvoke(new Action(() => { lblClientCount.Text = $@"{DateTime.Now} 连接总数：{s.Result.Count}"; }));
            });

            _mqttServer.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(e =>
             {
                 listBox1.BeginInvoke(_updateListBoxAction,
                     $"{DateTime.Now} ClientId:{e.ClientId} Topic:{e.ApplicationMessage.Topic} Payload:{Encoding.UTF8.GetString(e.ApplicationMessage.Payload)} QualityOfServiceLevel:{e.ApplicationMessage.QualityOfServiceLevel}");
             });

            _mqttServer.ClientSubscribedTopicHandler = new MqttServerClientSubscribedHandlerDelegate(e =>
             {
                 listBox1.BeginInvoke(_updateListBoxAction, $"{DateTime.Now} Client subscribed topic. ClientId:{e.ClientId} Topic:{e.TopicFilter.Topic} QualityOfServiceLevel:{e.TopicFilter.QualityOfServiceLevel}");
             });

            _mqttServer.ClientUnsubscribedTopicHandler = new MqttServerClientUnsubscribedTopicHandlerDelegate(e =>
            {
                listBox1.BeginInvoke(_updateListBoxAction, $"{DateTime.Now} Client unsubscribed topic. ClientId:{e.ClientId} Topic:{e.TopicFilter.Length}");
            });

            _mqttServer.StartedHandler = new MqttServerStartedHandlerDelegate(e =>
            {
                listBox1.BeginInvoke(_updateListBoxAction, $"{DateTime.Now} Mqtt Server Started...");
            });

            _mqttServer.StoppedHandler = new MqttServerStoppedHandlerDelegate(e =>
             {
                 listBox1.BeginInvoke(_updateListBoxAction, $"{DateTime.Now} Mqtt Server Stopped...");
             });

            await _mqttServer.StartAsync(optionBuilder.Build());
        }
    }
}
