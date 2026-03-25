using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitPlugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ToggleSafeModeCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            App.SafeMode = !App.SafeMode;
            
            if (App.SafeModeButton != null)
            {
                App.SafeModeButton.ItemText = App.SafeMode ? "Safe Mode\n[ON]" : "Safe Mode\n[WRITE]";
            }
            
            string status = App.SafeMode ? "ENABLED (Read-Only)" : "DISABLED (Write Access Allowed)";
            TaskDialog.Show("MCP Safe Mode", $"Safe Mode is now {status}.");

            return Result.Succeeded;
        }
    }
}
