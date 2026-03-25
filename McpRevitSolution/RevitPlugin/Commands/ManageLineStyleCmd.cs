using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitPlugin.Attributes;
using RevitPlugin.Core;
using System;

namespace RevitPlugin.Commands
{
    [McpTool("ManageLineStyle",
        "Creates, updates, or deletes Line Styles in the document. " +
        "REQUIRES A TRANSACTION. Line Styles are used to draw detail lines and model lines. " +
        "SCENARIOS:\n" +
        "1. Create: Action='Create', Name='AI_Red_Dashed', LineWeight=3, ColorHex='#FF0000', LinePatternId=12345\n" +
        "2. Update: Action='Update', Name='AI_Red_Dashed', LineWeight=5\n" +
        "3. Delete: Action='Delete', Name='AI_Red_Dashed'",
        requiresTransaction: true)]
    public class ManageLineStyleCmd : IRevitCommand
    {
        public class Args
        {
            [McpArg("The Action to perform. Allowed values: 'Create', 'Update', 'Delete'.", required: true)]
            public string Action { get; set; }

            [McpArg("The exact Name of the Line Style (e.g., 'Thin Lines', 'Overhead', 'My Custom Style').", required: true)]
            public string Name { get; set; }

            [McpArg("For Create/Update: The integer line weight (usually 1-16).", required: false)]
            public int? LineWeight { get; set; }

            [McpArg("For Create/Update: The Hex color string (e.g., '#FF0000' for Red, '#000000' for Black).", required: false)]
            public string ColorHex { get; set; }

            [McpArg("For Create/Update: The ElementId (long) of the LinePatternElement. Use GetGraphicsSettings to find this ID.", required: false)]
            public long? LinePatternId { get; set; }
        }

        public McpResponse Execute(Autodesk.Revit.UI.UIApplication app, JObject jsonArgs)
        {
            Autodesk.Revit.DB.Document doc = app.ActiveUIDocument.Document;
            try
            {
                var args = jsonArgs.ToObject<Args>();
                string action = args.Action.ToLower();

                // Get the main Lines category
                Category linesCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                
                // Check if the subcategory already exists
                Category subCat = null;
                if (linesCat.SubCategories.Contains(args.Name))
                {
                    subCat = linesCat.SubCategories.get_Item(args.Name);
                }

                if (action == "delete")
                {
                    if (subCat == null) return new McpResponse { Success = false, Message = $"Line Style '{args.Name}' does not exist." };
                    
                    doc.Delete(subCat.Id); // Deleting the category deletes the line style
                    return new McpResponse { Success = true, Message = $"Successfully deleted Line Style '{args.Name}'." };
                }
                else if (action == "create")
                {
                    if (subCat != null) return new McpResponse { Success = false, Message = $"Line Style '{args.Name}' already exists. Use 'Update' instead." };
                    
                    subCat = doc.Settings.Categories.NewSubcategory(linesCat, args.Name);
                }
                else if (action == "update")
                {
                    if (subCat == null) return new McpResponse { Success = false, Message = $"Line Style '{args.Name}' not found. Use 'Create' instead." };
                }
                else
                {
                    return new McpResponse { Success = false, Message = $"Invalid Action: '{args.Action}'." };
                }

                // Apply properties if provided
                if (args.LineWeight.HasValue)
                {
                    subCat.SetLineWeight(args.LineWeight.Value, GraphicsStyleType.Projection);
                }

                if (!string.IsNullOrEmpty(args.ColorHex))
                {
                    Color newColor = HexToColor(args.ColorHex);
                    if (newColor != null) subCat.LineColor = newColor;
                }

                if (args.LinePatternId.HasValue)
                {
                    subCat.SetLinePatternId(new ElementId(args.LinePatternId.Value), GraphicsStyleType.Projection);
                }

                return new McpResponse { Success = true, Message = $"Successfully {action}d Line Style '{args.Name}'." };
            }
            catch (Exception ex)
            {
                return new McpResponse { Success = false, Message = $"Error managing line style: {ex.Message}" };
            }
        }

        // Helper to convert Hex string to Revit Color
        private Color HexToColor(string hex)
        {
            try
            {
                hex = hex.Replace("#", "");
                if (hex.Length != 6) return null;

                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);

                return new Color(r, g, b);
            }
            catch
            {
                return null;
            }
        }
    }
}