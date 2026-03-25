using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitPlugin.Attributes;
using RevitPlugin.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitPlugin.Commands
{
    [McpTool("GetGraphicsSettings",
        "Retrieves document-level graphic settings including Line Styles, Line Patterns, Fill Patterns, and Materials. " +
        "SCENARIOS:\n" +
        "1. Audit Line Styles: Set 'SettingType' to 'LineStyles' to see all available line weights and colors.\n" +
        "2. Find a Pattern: Set 'SettingType' to 'LinePatterns' to get the ID needed to assign a dashed line to a new Line Style.")]
    public class GetGraphicsSettingsCmd : IRevitCommand
    {
        public class Args
        {
            [McpArg("The type of graphic setting to retrieve. Allowed values: 'LineStyles', 'LinePatterns', 'FillPatterns', 'Materials'.", required: true)]
            public string SettingType { get; set; }

            [McpArg("Optional text filter (case-insensitive) to find a specific style/pattern by Name.", required: false)]
            public string Name { get; set; }
        }

        public McpResponse Execute(Autodesk.Revit.UI.UIApplication app, JObject jsonArgs)
        {
            Autodesk.Revit.DB.Document doc = app.ActiveUIDocument.Document;
            try
            {
                var args = jsonArgs.ToObject<Args>();
                var results = new List<object>();
                string filterName = args.Name?.ToLower() ?? "";

                switch (args.SettingType.ToLower())
                {
                    case "linestyles":
                        // Line Styles are subcategories of the Lines category
                        Category linesCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                        foreach (Category subCat in linesCat.SubCategories)
                        {
                            if (!string.IsNullOrEmpty(filterName) && !subCat.Name.ToLower().Contains(filterName)) continue;

                            // Find the LinePattern name (if it's not Solid)
                            string patternName = "Solid";
                            if (subCat.GetLinePatternId(GraphicsStyleType.Projection) != ElementId.InvalidElementId)
                            {
                                var patternElem = doc.GetElement(subCat.GetLinePatternId(GraphicsStyleType.Projection));
                                if (patternElem != null) patternName = patternElem.Name;
                            }

                            results.Add(new
                            {
                                Name = subCat.Name,
                                Id = subCat.Id.Value, // The Category ID
                                LineWeight = subCat.GetLineWeight(GraphicsStyleType.Projection),
                                Color = $"RGB({subCat.LineColor.Red}, {subCat.LineColor.Green}, {subCat.LineColor.Blue})",
                                LinePattern = patternName,
                                LinePatternId = subCat.GetLinePatternId(GraphicsStyleType.Projection).Value
                            });
                        }
                        break;

                    case "linepatterns":
                        var linePatterns = new FilteredElementCollector(doc).OfClass(typeof(LinePatternElement)).ToElements();
                        foreach (var lp in linePatterns)
                        {
                            if (!string.IsNullOrEmpty(filterName) && !lp.Name.ToLower().Contains(filterName)) continue;
                            results.Add(new { Name = lp.Name, Id = lp.Id.Value });
                        }
                        break;

                    case "fillpatterns":
                        var fillPatterns = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>();
                        foreach (var fp in fillPatterns)
                        {
                            if (!string.IsNullOrEmpty(filterName) && !fp.Name.ToLower().Contains(filterName)) continue;
                            results.Add(new 
                            { 
                                Name = fp.Name, 
                                Id = fp.Id.Value, 
                                Target = fp.GetFillPattern().Target == FillPatternTarget.Drafting ? "Drafting" : "Model" 
                            });
                        }
                        break;

                    case "materials":
                        var materials = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>();
                        foreach (var mat in materials)
                        {
                            if (!string.IsNullOrEmpty(filterName) && !mat.Name.ToLower().Contains(filterName)) continue;
                            results.Add(new 
                            { 
                                Name = mat.Name, 
                                Id = mat.Id.Value,
                                Color = $"RGB({mat.Color.Red}, {mat.Color.Green}, {mat.Color.Blue})"
                            });
                        }
                        break;

                    default:
                        return new McpResponse { Success = false, Message = $"Unsupported SettingType: '{args.SettingType}'." };
                }

                return new McpResponse
                {
                    Success = true,
                    Message = $"Found {results.Count} {args.SettingType}.",
                    Data = results
                };
            }
            catch (Exception ex)
            {
                return new McpResponse { Success = false, Message = $"Error retrieving graphic settings: {ex.Message}" };
            }
        }
    }
}