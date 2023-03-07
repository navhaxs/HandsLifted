using System.Text;

namespace HandsLiftedApp.Logic
{
    using Avalonia.Threading;
    using EmbedIO.WebSockets;
    using HandsLiftedApp.Models.AppState;
    using HandsLiftedApp.Models.UI;
    using Newtonsoft.Json.Linq;
    using ReactiveUI;
    using System;
    using System.Threading;
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
            //Debug.Print("OnMessageReceivedAsync");
            try
            {
                // parse message
                string buffer = Encoding.GetString(rxBuffer);
                JObject jsonData = JObject.Parse(buffer);

                //Debug.Print(jsonData.ToString());
                switch (jsonData["action"].ToString())
                {
                    case nameof(ActionMessage.NavigateSlideAction.NextSlide):
                        new Thread(() =>
                        {
                            Thread.CurrentThread.IsBackground = true;
                            /* run your code here */
                            Dispatcher.UIThread.InvokeAsync(() => {
                                MessageBus.Current.SendMessage(new ActionMessage() { Action = ActionMessage.NavigateSlideAction.NextSlide });
                                MessageBus.Current.SendMessage(new FocusSelectedItem()); // config item
                            });
                        }).Start();
                        return SendAsync(context, "{\"action\":\"NextSlide\", \"status\": \"ok\"}");
                        break;

                    case nameof(ActionMessage.NavigateSlideAction.PreviousSlide):
                        new Thread(() =>
                        {
                            Thread.CurrentThread.IsBackground = true;
                            /* run your code here */
                            Dispatcher.UIThread.InvokeAsync(() => {
                                MessageBus.Current.SendMessage(new ActionMessage() { Action = ActionMessage.NavigateSlideAction.PreviousSlide });
                                MessageBus.Current.SendMessage(new FocusSelectedItem()); // config item
                            });
                        }).Start();
                        return SendAsync(context, "{\"action\":\"PreviousSlide\", \"status\": \"ok\"}");
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
