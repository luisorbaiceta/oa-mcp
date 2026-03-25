using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitPlugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ToggleServerCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (App.Server == null)
            {
                TaskDialog.Show("MCP Server", "Server is not initialized.");
                return Result.Failed;
            }

            if (App.Server.IsListening)
            {
                App.Server.Stop();
                if (App.ServerButton != null)
                {
                    App.ServerButton.ItemText = "Server\n[OFF]";
                }
                TaskDialog.Show("MCP Server", "MCP Server Stopped.");
            }
            else
            {
                App.Server.Start();
                if (App.ServerButton != null)
                {
                    App.ServerButton.ItemText = "Server\n[ON]";
                }
                TaskDialog.Show("MCP Server", "MCP Server Started.");
            }
            
            return Result.Succeeded;
        }
    }
}
