using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitPlugin.Attributes;
using RevitPlugin.Core;
using System;

namespace RevitPlugin.Commands
{
    [McpTool("SetGraphicOverrides",
        "Modifies the VIEW-SPECIFIC graphical overrides for a Category (Visibility/Graphics menu). " +
        "REQUIRES A TRANSACTION. Changes made here ONLY affect the specified view. " +
        "SCENARIOS:\n" +
        "1. Highlight: Make all 'OST_Walls' red with a heavy line weight in a specific floor plan.\n" +
        "2. Halftone: Set 'Halftone'=true for 'OST_Floors' to fade them into the background.\n" +
        "3. Hide: Set 'IsHidden'=true to turn off the category in the view.",
        requiresTransaction: true)]
    public class SetGraphicOverridesCmd : IRevitCommand
    {
        public class Args
        {
            [McpArg("The ElementId (long) of the View to apply the overrides to.", required: true)]
            public long ViewId { get; set; }

            [McpArg("The BuiltInCategory to override (e.g., 'OST_Walls').", required: true)]
            public string Category { get; set; }

            [McpArg("If true, hides the category entirely in this view.", required: false)]
            public bool? IsHidden { get; set; }

            [McpArg("If true, applies Halftone to the category in this view.", required: false)]
            public bool? Halftone { get; set; }

            [McpArg("Hex color string (e.g., '#FF0000') for Projection lines.", required: false)]
            public string ProjectionLineColorHex { get; set; }

            [McpArg("Integer line weight (1-16) for Projection lines.", required: false)]
            public int? ProjectionLineWeight { get; set; }

            [McpArg("Hex color string for Cut lines.", required: false)]
            public string CutLineColorHex { get; set; }

            [McpArg("Integer line weight (1-16) for Cut lines.", required: false)]
            public int? CutLineWeight { get; set; }
            
            [McpArg("Set to 'true' to completely clear/reset all overrides for this category in this view.", required: false)]
            public bool ClearOverrides { get; set; } = false;
        }

        public McpResponse Execute(Autodesk.Revit.UI.UIApplication app, JObject jsonArgs)
        {
            Autodesk.Revit.DB.Document doc = app.ActiveUIDocument.Document;
            try
            {
                var args = jsonArgs.ToObject<Args>();

                View view = doc.GetElement(new ElementId(args.ViewId)) as View;
                if (view == null) return new McpResponse { Success = false, Message = "Invalid ViewId." };

                if (!Enum.TryParse(args.Category, true, out BuiltInCategory bic))
                    return new McpResponse { Success = false, Message = $"Invalid category: {args.Category}" };

                ElementId catId = new ElementId(bic);

                // Hide/Unhide Category
                if (args.IsHidden.HasValue)
                {
                    if (view.CanCategoryBeHidden(catId))
                        view.SetCategoryHidden(catId, args.IsHidden.Value);
                }

                // Clear Overrides if requested
                if (args.ClearOverrides)
                {
                    view.SetCategoryOverrides(catId, new OverrideGraphicSettings());
                    return new McpResponse { Success = true, Message = $"Cleared all overrides for {args.Category} in view {view.Name}." };
                }

                // Get existing overrides to modify them instead of wiping them
                OverrideGraphicSettings ogs = view.GetCategoryOverrides(catId);

                if (args.Halftone.HasValue)
                    ogs.SetHalftone(args.Halftone.Value);

                if (!string.IsNullOrEmpty(args.ProjectionLineColorHex))
                {
                    Color c = HexToColor(args.ProjectionLineColorHex);
                    if (c != null) ogs.SetProjectionLineColor(c);
                }

                if (args.ProjectionLineWeight.HasValue)
                    ogs.SetProjectionLineWeight(args.ProjectionLineWeight.Value);

                if (!string.IsNullOrEmpty(args.CutLineColorHex))
                {
                    Color c = HexToColor(args.CutLineColorHex);
                    if (c != null) ogs.SetCutLineColor(c);
                }

                if (args.CutLineWeight.HasValue)
                    ogs.SetCutLineWeight(args.CutLineWeight.Value);

                // Apply the modified settings back to the view
                view.SetCategoryOverrides(catId, ogs);

                return new McpResponse { Success = true, Message = $"Successfully applied graphic overrides to '{args.Category}' in view '{view.Name}'." };
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