using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitPlugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ShowMcpInfoCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string mcpConfig = "Añade este bloque a tu archivo claude_desktop_config.json:\n\n" +
                               "\"revit\": {\n" +
                               "      \"command\": \"node\",\n" +
                               "      \"args\": [\n" +
                               "        \"Ruta a carpeta de addins de Revit\\\\index.js\"\n" +
                               "      ],\n" +
                               "      \"disabled\": false\n" +
                               "}";

            TaskDialog.Show("MCP Server Configuration", mcpConfig);

            return Result.Succeeded;
        }
    }
}
