using Autodesk.Revit.UI;
using RevitPlugin.Core;
using System.Reflection;

namespace RevitPlugin
{
    public class App : IExternalApplication
    {
        private static SocketServer _server;
        public static SocketServer Server => _server;
        
        public static bool SafeMode { get; set; } = true;
        
        public static PushButton ServerButton { get; set; }
        public static PushButton SafeModeButton { get; set; }
        
        private static Dispatcher _dispatcher;
        private static ExternalEvent _externalEvent;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Create Ribbon
                string tabName = "MCP Integration";
                try
                {
                    application.CreateRibbonTab(tabName);
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    // Tab already exists, ignore
                }

                RibbonPanel panel = application.CreateRibbonPanel(tabName, "Server Control");
                
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                
                PushButtonData serverButtonData = new PushButtonData(
                    "ToggleMcpServer",
                    "Server\n[OFF]",
                    assemblyPath,
                    "RevitPlugin.Commands.ToggleServerCmd");
                serverButtonData.ToolTip = "Start or Stop the MCP Socket Server.";
                
                PushButtonData safeModeButtonData = new PushButtonData(
                    "ToggleSafeMode",
                    "Safe Mode\n[ON]",
                    assemblyPath,
                    "RevitPlugin.Commands.ToggleSafeModeCmd");
                safeModeButtonData.ToolTip = "Enable or disable Safe Mode (Read-Only).";

                PushButtonData mcpInfoButtonData = new PushButtonData(
                    "ShowMcpInfo",
                    "MCP\nInfo",
                    assemblyPath,
                    "RevitPlugin.Commands.ShowMcpInfoCmd");
                mcpInfoButtonData.ToolTip = "Show MCP Server JSON configuration.";

                ServerButton = panel.AddItem(serverButtonData) as PushButton;
                SafeModeButton = panel.AddItem(safeModeButtonData) as PushButton;
                panel.AddItem(mcpInfoButtonData);

                // Generate Schema
                SchemaGenerator.GenerateManifest();

                // Setup Dispatcher & External Event
                _dispatcher = new Dispatcher();
                _externalEvent = ExternalEvent.Create(_dispatcher);

                // Initialize Socket Server (but do not start it automatically)
                _server = new SocketServer(_externalEvent, _dispatcher);

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("RevitMcp Error", ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            _server?.Stop();
            return Result.Succeeded;
        }
    }
}
