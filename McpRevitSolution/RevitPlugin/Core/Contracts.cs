using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace RevitPlugin.Core
{
    public class McpResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
    }

    public interface IRevitCommand
    {
        McpResponse Execute(UIApplication app, JObject jsonArgs);
    }
}
