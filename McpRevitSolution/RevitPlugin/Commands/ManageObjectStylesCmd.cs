using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitPlugin.Attributes;
using RevitPlugin.Core;
using System;

namespace RevitPlugin.Commands
{
    [McpTool("ManageObjectStyles",
        "Modifies the PROJECT-WIDE graphical settings for a Category or Subcategory (Object Styles). " +
        "REQUIRES A TRANSACTION. Changes made here affect the entire model unless overridden in a specific view. " +
        "SCENARIOS:\n" +
        "1. Main Category: Set 'Category'='OST_Doors', 'LineColorHex'='#FF0000' to make all doors red by default.\n" +
        "2. Subcategory: Set 'Category'='OST_Doors', 'SubCategory'='Panel' to only change the door panel style.\n" +
        "3. Line Weights: Can specify both Cut and Projection line weights (1-16).",
        requiresTransaction: true)]
    public class ManageObjectStylesCmd : IRevitCommand
    {
        public class Args
        {
            [McpArg("The BuiltInCategory to modify (e.g., 'OST_Walls', 'OST_Doors').", required: true)]
            public string Category { get; set; }

            [McpArg("Optional. The name of a Subcategory to modify (e.g., 'Hidden Lines', 'Glass'). " +
                    "If null, modifies the main category.", required: false)]
            public string SubCategory { get; set; }

            [McpArg("The integer line weight (1-16) for elements seen in projection/surface.", required: false)]
            public int? ProjectionLineWeight { get; set; }

            [McpArg("The integer line weight (1-16) for elements when cut by a view plane.", required: false)]
            public int? CutLineWeight { get; set; }

            [McpArg("The Hex color string (e.g., '#FF0000' for Red).", required: false)]
            public string LineColorHex { get; set; }

            [McpArg("The ElementId (long) of a Material to apply as the default for this category.", required: false)]
            public long? MaterialId { get; set; }
        }

        public McpResponse Execute(Autodesk.Revit.UI.UIApplication app, JObject jsonArgs)
        {
            Autodesk.Revit.DB.Document doc = app.ActiveUIDocument.Document;
            try
            {
                var args = jsonArgs.ToObject<Args>();

                if (!Enum.TryParse(args.Category, true, out BuiltInCategory bic))
                {
                    return new McpResponse { Success = false, Message = $"Invalid category: {args.Category}" };
                }

                Categories categories = doc.Settings.Categories;
                Category targetCategory = categories.get_Item(bic);

                if (targetCategory == null)
                    return new McpResponse { Success = false, Message = "Category not found in document." };

                // Navigate to Subcategory if requested
                if (!string.IsNullOrEmpty(args.SubCategory))
                {
                    if (targetCategory.SubCategories.Contains(args.SubCategory))
                    {
                        targetCategory = targetCategory.SubCategories.get_Item(args.SubCategory);
                    }
                    else
                    {
                        return new McpResponse { Success = false, Message = $"Subcategory '{args.SubCategory}' not found under '{args.Category}'." };
                    }
                }

                // Apply Overrides
                if (args.ProjectionLineWeight.HasValue)
                    targetCategory.SetLineWeight(args.ProjectionLineWeight.Value, GraphicsStyleType.Projection);

                if (args.CutLineWeight.HasValue && targetCategory.HasMaterialQuantities) // Ensure category can be cut
                    targetCategory.SetLineWeight(args.CutLineWeight.Value, GraphicsStyleType.Cut);

                if (!string.IsNullOrEmpty(args.LineColorHex))
                {
                    Color newColor = HexToColor(args.LineColorHex);
                    if (newColor != null) targetCategory.LineColor = newColor;
                }

                if (args.MaterialId.HasValue)
                {
                    var mat = doc.GetElement(new ElementId(args.MaterialId.Value)) as Material;
                    if (mat != null) targetCategory.Material = mat;
                }

                return new McpResponse { Success = true, Message = $"Successfully updated Object Styles for '{targetCategory.Name}'." };
            }
            catch (Exception ex)
            {
                return new McpResponse { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        private Color HexToColor(string hex)
        {
            try
            {
                hex = hex.Replace("#", "");
                if (hex.Length != 6) return null;
                return new Color(
                    Convert.ToByte(hex.Substring(0, 2), 16),
                    Convert.ToByte(hex.Substring(2, 2), 16),
                    Convert.ToByte(hex.Substring(4, 2), 16));
            }
            catch { return null; }
        }
    }
}