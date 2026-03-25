using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitPlugin.Attributes;

namespace RevitPlugin.Core
{
    public class Dispatcher : IExternalEventHandler
    {
        private class Request
        {
            public string CommandName { get; set; }
            public JObject Args { get; set; }
            public TaskCompletionSource<McpResponse> Tcs { get; set; }
        }

        private readonly ConcurrentQueue<Request> _queue = new ConcurrentQueue<Request>();
        private readonly Dictionary<string, Type> _commandTypes;

        public Dispatcher()
        {
            _commandTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetCustomAttribute<McpToolAttribute>() != null && typeof(IRevitCommand).IsAssignableFrom(t))
                .ToDictionary(t => t.GetCustomAttribute<McpToolAttribute>().Name, t => t);
        }

        public void Enqueue(string commandName, JObject args, TaskCompletionSource<McpResponse> tcs)
        {
            _queue.Enqueue(new Request { CommandName = commandName, Args = args, Tcs = tcs });
        }

        public void Execute(UIApplication app)
        {
            // Process currently queued items (all pending)
            // Or just one? If we process all, we might block UI if spamming.
            // Let's process until empty for now.
            while (_queue.TryDequeue(out var request))
            {
                try
                {
                    if (!_commandTypes.TryGetValue(request.CommandName, out var commandType))
                    {
                        request.Tcs.SetResult(new McpResponse { Success = false, Message = $"Command '{request.CommandName}' not found." });
                        continue;
                    }

                    var command = (IRevitCommand)Activator.CreateInstance(commandType);
                    var mcpToolAttr = commandType.GetCustomAttribute<McpToolAttribute>();

                    if (App.SafeMode)
                    {
                        if (mcpToolAttr.RequiresTransaction || request.CommandName == "ExecuteCSharp")
                        {
                            request.Tcs.SetResult(new McpResponse { Success = false, Message = $"Safe Mode is enabled. The tool '{request.CommandName}' is blocked from executing." });
                            continue;
                        }
                    }

                    if (mcpToolAttr != null && mcpToolAttr.RequiresTransaction)
                    {

                        using (var trans = new Transaction(app.ActiveUIDocument.Document, $"MCP: {request.CommandName}"))
                        {
                            trans.Start();
                            var response = command.Execute(app, request.Args);
                            
                            if (response.Success)
                                trans.Commit();
                            else
                                trans.RollBack();

                            request.Tcs.SetResult(response);
                        }
                    }
                    else
                    {
                        // Execute without transaction
                        var response = command.Execute(app, request.Args);
                        request.Tcs.SetResult(response);
                    }
                }
                catch (Exception ex)
                {
                    request.Tcs.SetResult(new McpResponse { Success = false, Message = $"Execute Error: {ex.Message}" });
                }
            }
        }

        public string GetName() => "McpDispatcher";
    }
}
