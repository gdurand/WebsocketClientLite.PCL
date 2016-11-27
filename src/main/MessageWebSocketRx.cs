﻿using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using HttpMachine;
using ISocketLite.PCL.Interface;
using ISocketLite.PCL.Model;
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
        private IDisposable _outerCancellationRegiration;

<<<<<<< Updated upstream
        private CancellationTokenSource _cancellationTokenSource;

        private IConnectableObservable<byte[]> ObservableWebsocketData => Observable.While(
                    () => !_cancellationTokenSource.IsCancellationRequested,
                    Observable.FromAsync(ReadOneByteAtTheTimeAsync))
            .SubscribeOn(Scheduler.Default)
            .Publish();

        public IObservable<string> ObserveTextMessagesReceived => _websocketListener.ObserveTextMessageSequence;



=======
        //private CancellationTokenSource _outerCancellationTokenSource;
        private CancellationTokenSource _innerCancellationTokenSource;

        public IObservable<string> ObserveTextMessagesReceived => _websocketListener.ObserveTextMessageSequence;

>>>>>>> Stashed changes
        public bool IsConnected { get; private set; }
        public bool SubprotocolAccepted { get; private set; }

        public string SubprotocolAcceptedName { get; private set; }


<<<<<<< Updated upstream
            if (bytesRead < oneByteArray.Length)
            {
                _cancellationTokenSource.Cancel();
                throw new Exception("Web socket connection aborted unexpectantly. Check connection and socket security version/TLS version)");
            }
            return oneByteArray;
        }
=======
>>>>>>> Stashed changes

        public MessageWebSocketRx()
        {
            _webSocketConnectService = new WebSocketConnectService();
            _websocketSenderService = new WebsocketSenderService();

            _websocketListener = new WebsocketListener(
                //_tcpSocketClient,
                _webSocketConnectService);
        }

        public void SetRequestHeader(string headerName, string headerValue)
        {
            throw new NotImplementedException();
        }

        public async Task ConnectAsync(
            Uri uri, 
            CancellationTokenSource outerCancellationTokenSource, 
            IEnumerable<string> subprotocols = null,
            bool ignoreServerCertificateErrors = false,
            TlsProtocolVersion tlsProtocolVersion = TlsProtocolVersion.Tls12)
        {
            _outerCancellationRegiration = outerCancellationTokenSource.Token.Register(() =>
            {
                _innerCancellationTokenSource.Cancel();
            });

            _innerCancellationTokenSource = new CancellationTokenSource();

            using (_httpParserDelegate = new HttpParserDelegate())
            using (_httpParserHandler = new HttpCombinedParser(_httpParserDelegate))
            {
                var isSecure = IsSecureWebsocket(uri);

                try
                {
                    await _webSocketConnectService.ConnectAsync(
                    uri,
                    isSecure,
                    _httpParserDelegate,
                    _httpParserHandler,
                    _innerCancellationTokenSource,
                    _websocketListener,
                    subprotocols,
                    ignoreServerCertificateErrors,
                    tlsProtocolVersion);
                }
                catch (Exception ex)
                {
                    throw ex;
                }

                if (_httpParserDelegate.HttpRequestReponse.StatusCode == 101)
                {
                    if (subprotocols != null)
                    {
                        SubprotocolAccepted = _httpParserDelegate?.HttpRequestReponse?.Headers?.ContainsKey("SEC-WEBSOCKET-PROTOCOL") ?? false;

                        if (SubprotocolAccepted)
                        {
                            SubprotocolAcceptedName = _httpParserDelegate?.HttpRequestReponse?.Headers?["SEC-WEBSOCKET-PROTOCOL"];
                            if (!string.IsNullOrEmpty(SubprotocolAcceptedName))
                            {
                                IsConnected = true;
                            }
                            else
                            {
                                throw new Exception("Server responded with blank Sub Protocol name");
                            }
                        }
                        else
                        {
                            throw new Exception("Server did not support any of the needed Sub Protocols");
                        }
                    }
                    else
                    {
                        IsConnected = true;
                    }
                    _websocketListener.DataReceiveMode = DataReceiveMode.IsListeningForTextData;
                }
            }
        }

        public async Task SendTextAsync(string message)
        {
            if (IsConnected)
            {
                await _websocketSenderService.SendTextAsync(_webSocketConnectService.TcpSocketClient, message);
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
                await _websocketSenderService.SendTextMultiFrameAsync(_webSocketConnectService.TcpSocketClient, message, frameType);
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
                await _websocketSenderService.SendTextAsync(_webSocketConnectService.TcpSocketClient, messageList);
            }
            else
            {
                throw new Exception("Not connected. Client must beconnected to websocket server before sending message");
            }
        }
        public async Task CloseAsync()
        {
            _outerCancellationRegiration.Dispose();

            if (_tcpSocketClient.IsConnected)
            {
                await _websocketSenderService.SendCloseHandshake(_webSocketConnectService.TcpSocketClient, StatusCodes.GoingAway);
            }

<<<<<<< Updated upstream
                if (!_websocketListener.HasReceivedCloseFromServer)
                {
                    _websocketListener.Stop();
                }
            }).ConfigureAwait(false);
=======
            // Give server a chance to respond to close
            await Task.Delay(TimeSpan.FromMilliseconds(100));

            _websocketListener.Stop();

            // If Server does not close the connection, close it after 2 sec.
            //TODO There most be a more elegant way to do this?
            //Task.Run(async () =>
            //{
            //    await Task.Delay(TimeSpan.FromSeconds(2));

            //    if (!_websocketListener.HasReceivedCloseFromServer)
            //    {
            //        IsConnected = false;
            //        _websocketListener.Stop();
            //    }
            //}).ConfigureAwait(false);
>>>>>>> Stashed changes
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
