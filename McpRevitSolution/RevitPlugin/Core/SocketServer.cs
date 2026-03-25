using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitPlugin.Core
{
    public class SocketServer
    {
        private TcpListener _listener;
        private bool _isRunning;
        private readonly ExternalEvent _externalEvent;
        private readonly Dispatcher _dispatcher;

        public SocketServer(ExternalEvent externalEvent, Dispatcher dispatcher)
        {
            _externalEvent = externalEvent;
            _dispatcher = dispatcher;
        }

        public bool IsListening => _isRunning;

        public void Start(int port = 5000)
        {
            if (_isRunning) return;

            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            _isRunning = true;
            Task.Run(ListenLoop);
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
        }

        private async Task ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = HandleClient(client);
                }
                catch (Exception ex)
                {
                    // Log error
                    System.Diagnostics.Debug.WriteLine($"Socket Listener Error: {ex.Message}");
                }
            }
        }

        private async Task HandleClient(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true })
            {
                try
                {
                    var json = await reader.ReadToEndAsync();
                    var request = JObject.Parse(json);

                    var commandName = request["command"]?.ToString();
                    var args = request["args"] as JObject;

                    if (string.IsNullOrEmpty(commandName))
                    {
                         await writer.WriteAsync(JsonConvert.SerializeObject(new McpResponse { Success = false, Message = "Invalid request: missing command name" }));
                         return;
                    }

                    // Set state in dispatcher
                    var tcs = new TaskCompletionSource<McpResponse>();
                    _dispatcher.Enqueue(commandName, args, tcs);
                    
                    // Raise external event
                    _externalEvent.Raise();

                    // Wait for Revit to process
                    var response = await tcs.Task;
                    
                    await writer.WriteAsync(JsonConvert.SerializeObject(response));
                }
                catch (Exception ex)
                {
                    var errorResponse = new McpResponse { Success = false, Message = $"Server Error: {ex.Message}" };
                    await writer.WriteAsync(JsonConvert.SerializeObject(errorResponse));
                }
            }
        }
    }
}
