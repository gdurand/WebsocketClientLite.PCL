﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using HttpMachine;
using ISocketLite.PCL.Interface;
using IWebsocketClientLite.PCL;
using SocketLite.Services;
using WebsocketClientLite.PCL.Model;
using WebsocketClientLite.PCL.Parser;
using WebsocketClientLite.PCL.Service;

namespace WebsocketClientLite.PCL
{
    public class MessageWebSocketRx : IMessageWebSocketRx
    {
        private readonly ITcpSocketClient _tcpSocketClient = new TcpSocketClient();
        private readonly WebSocketConnectService _webSocketConnectService;
        private readonly WebsocketListener _websocketListener;
        private readonly WebsocketSenderService _websocketSenderService;

        private HttpParserDelegate _httpParserDelegate;
        private HttpCombinedParser _httpParserHandler;

        private CancellationTokenSource _cancellationTokenSource;

        private IConnectableObservable<byte[]> ObservableWebsocketData => Observable.While(
                    () => !_cancellationTokenSource.IsCancellationRequested,
                    Observable.FromAsync(ReadOneByteAtTheTimeAsync))
            .SubscribeOn(Scheduler.Default)
            .Publish();

        public IObservable<string> ObserveTextMessagesReceived => _websocketListener.ObserveTextMessageSequence;



        public bool IsConnected { get; private set; }
        public bool SubprotocolAccepted { get; private set; }

        public string SubprotocolAcceptedName { get; private set; } 

        private async Task<byte[]> ReadOneByteAtTheTimeAsync()
        {
            var oneByteArray = new byte[1];

            var bytesRead = await _tcpSocketClient.ReadStream.ReadAsync(oneByteArray, 0, 1);

            if (bytesRead < oneByteArray.Length)
            {
                _cancellationTokenSource.Cancel();
                throw new Exception("Web socket connection aborted unexpectantly");
            }
            return oneByteArray;
        }

        public MessageWebSocketRx()
        {
            _webSocketConnectService = new WebSocketConnectService(_tcpSocketClient);
            _websocketSenderService = new WebsocketSenderService(_tcpSocketClient);

            _websocketListener = new WebsocketListener(
                _tcpSocketClient,
                ObservableWebsocketData,
                _webSocketConnectService);
        }

        public void SetRequestHeader(string headerName, string headerValue)
        {
            throw new NotImplementedException();
        }

        public async Task ConnectAsync(
            Uri uri, 
            CancellationTokenSource cts, 
            bool ignoreServerCertificateErrors = false, 
            IEnumerable<string> subprotocols = null)
        {
            _cancellationTokenSource = cts;
            _httpParserDelegate = new HttpParserDelegate();
            _httpParserHandler = new HttpCombinedParser(_httpParserDelegate);
            
            var isSecure = IsSecureWebsocket(uri);

            try
            {
                await _webSocketConnectService.ConnectAsync(
                uri,
                isSecure,
                _httpParserDelegate,
                _httpParserHandler,
                _cancellationTokenSource,
                _websocketListener,
                ignoreServerCertificateErrors,
                subprotocols);
            }
            catch (Exception)
            {
                throw;
            }

            if (_httpParserDelegate.HttpRequestReponse.StatusCode == 101)
            {
                if (subprotocols != null)
                {
                    SubprotocolAccepted = _httpParserDelegate.HttpRequestReponse.Headers.ContainsKey("Sec-WebSocket-Protocol");

                    if (SubprotocolAccepted)
                    {
                        SubprotocolAcceptedName = _httpParserDelegate.HttpRequestReponse.Headers["Sec-WebSocket-Protocol"];
                        IsConnected = true;
                    }
                    else
                    {
                        throw new Exception("Server did not support any of the proposed Sub Protocols");
                    }
                }
                else
                {
                    IsConnected = true;
                }
                
                _websocketListener.DataReceiveMode = DataReceiveMode.IsListeningForTextData;
            }
        }

        public async Task SendTextAsync(string message)
        {
            if (IsConnected)
            {
                await _websocketSenderService.SendTextAsync(message);
            }
            else
            {
                throw new Exception("Not connected. Client must beconnected to websocket server before sending message");
            }
        }

        public async Task SendTextMultiFrameAsync(string message, FrameType frameType)
        {
            if (IsConnected)
            {
                await _websocketSenderService.SendTextMultiFrameAsync(message, frameType);
            }
            else
            {
                throw new Exception("Not connected. Client must beconnected to websocket server before sending message");
            }
        }

        public async Task SendTextAsync(string[] messageList)
        {
            if (IsConnected)
            {
                await _websocketSenderService.SendTextAsync(messageList);
            }
            else
            {
                throw new Exception("Not connected. Client must beconnected to websocket server before sending message");
            }
        }


        public async Task CloseAsync()
        {
            await _websocketSenderService.SendCloseHandshake(StatusCodes.GoingAway);
            
            // If Server does not close the connection, close it after 2 sec.
            //TODO There most be a more elegant way to do this?
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2));

                if (!_websocketListener.HasReceivedCloseFromServer)
                {
                    _websocketListener.Stop();
                }
            }).ConfigureAwait(false);
        }

        private bool IsSecureWebsocket(Uri uri)
        {
            bool secure;

            switch (uri.Scheme.ToLower())
            {
                case "ws":
                    {
                        secure = false;
                        break;
                    }
                case "wss":
                    {
                        secure = true;
                        break; ;
                    }
                default: throw new ArgumentException("Uri is not Websocket kind.");
            }
            return secure;
        }

    }
}
