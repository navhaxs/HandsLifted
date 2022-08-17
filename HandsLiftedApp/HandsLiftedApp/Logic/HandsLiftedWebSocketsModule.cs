﻿using EmbedIO.WebSockets;
using HandsLiftedApp.Importer.PowerPoint;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HandsLiftedApp.Logic
{
    using EmbedIO.WebSockets;
    using HandsLiftedApp.Data.Models;
    using HandsLiftedApp.Models.AppState;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using ReactiveUI;
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    internal class HandsLiftedWebSocketsModule : WebSocketModule
    {
        public HandsLiftedWebSocketsModule(string urlPath)
            : base(urlPath, true)
        {
            // placeholder

            //WeakReferenceMessenger.Default.Register<ResponseMessage>(this, (object r, ResponseMessage eventMessages) =>
            //{
            //    string payload = "{\"message\": " + JsonConvert.ToString(eventMessages.WsResponseMessage) + "}";
            //    BroadcastAsync(payload);
            //});

            //WeakReferenceMessenger.Default.Register<StateUpdateMessage>(this, (object r, StateUpdateMessage stateUpdateMessage) =>
            //{
            //    string payload = "{\"response\":\"statusUpdate\", \"status\":" + JsonConvert.SerializeObject(stateUpdateMessage.state) + "}";
            //    BroadcastAsync(payload);
            //});

            //WeakReferenceMessenger.Default.Register<BroadcastMessage>(this, (object r, BroadcastMessage broadcastMessage) =>
            //{
            //    string payload = "{\"response\":\"broadcast\", \"message\":" + JsonConvert.SerializeObject(broadcastMessage.message)
            //                     + ", \"options\":" + JsonConvert.SerializeObject(broadcastMessage.options) +
            //                     "}";
            //    BroadcastAsync(payload);
            //});
        }

        /// <inheritdoc />
        protected override Task OnMessageReceivedAsync(
            IWebSocketContext context,
            byte[] rxBuffer,
            IWebSocketReceiveResult rxResult)
        {
            Debug.Print("OnMessageReceivedAsync");

            try
            {
                // parse message
                string buffer = Encoding.GetString(rxBuffer);
                JObject jsonData = JObject.Parse(buffer);

                Debug.Print(jsonData.ToString());
                switch (jsonData["action"].ToString())
                {
                    case nameof(ActionMessage.NavigateSlideAction.NextSlide):
                        MessageBus.Current.SendMessage(new ActionMessage() { Action = ActionMessage.NavigateSlideAction.NextSlide });
                        SendAsync(context, "{\"action\":\"NextSlide\", \"status\": \"ok\"}");
                        break;

                    case nameof(ActionMessage.NavigateSlideAction.PreviousSlide):
                        MessageBus.Current.SendMessage(new ActionMessage() { Action = ActionMessage.NavigateSlideAction.PreviousSlide });
                        SendAsync(context, "{\"action\":\"PreviousSlide\", \"status\": \"ok\"}");
                        break;

                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                return SendAsync(context, "{\"error\": \"Invalid command\"}");
            }
            // dummy response. should respond with error 'unknown command'
            //return SendToOthersAsync(context, Encoding.GetString(rxBuffer));


            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override Task OnClientConnectedAsync(IWebSocketContext context)
        {
            //Debug.Print("OnClientConnectedAsync");
            return SendAsync(context, "{\"hello\":\"there\"}");
            //return Task.CompletedTask;
        }
        //SendAsync(context, "{\"response\":\"statusUpdate\", \"status\":" + JsonConvert.SerializeObject(Main.api.GetStateInfo()) + "}");

        /// <inheritdoc />
        protected override Task OnClientDisconnectedAsync(IWebSocketContext context)
        {
            //Debug.Print("OnClientDisconnectedAsync");
            return Task.CompletedTask;
        }
    }
}
