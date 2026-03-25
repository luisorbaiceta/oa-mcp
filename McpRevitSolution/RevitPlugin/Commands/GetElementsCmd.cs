using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitPlugin.Attributes;
using RevitPlugin.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitPlugin.Commands
{
    [McpTool("GetElements",
        "The ultimate search tool for Revit elements. Use this to find ANY element, type, or instance in the model. " +
        "SCENARIOS:\n" +
        "1. Find Instances (Default): To find physical items (e.g., all Doors), set 'Category'='OST_Doors' and 'GetTypes'=false.\n" +
        "2. Find Types/Symbols: To find Family Types (e.g., a specific Wall Type), set 'Category'='OST_Walls' and 'GetTypes'=true.\n" +
        "3. Scoping by View: To find annotations or elements visible on a sheet/view, provide 'ViewId'. This is CRITICAL for view-specific elements.\n" +
        "4. Scoping by Level: To find elements on a specific level (e.g. Furniture on Level 1), provide 'LevelId'.\n" +
        "5. Specific Parameters: To find a door with Mark '101', use 'ParameterFilters'={'Mark':'101'}.\n" +
        "6. Performance: ALWAYS provide a 'Category' or 'Class' to speed up the search. Searching without them forces a scan of the ENTIRE database.")]
    public class GetElementsCmd : IRevitCommand
    {
        public class Args
        {
            [McpArg("The BuiltInCategory name (e.g., 'OST_Walls', 'OST_Rooms', 'OST_Levels'). " +
                    "This is the FASTEST and MOST PREFERRED way to filter. " +
                    "Consult the Revit API BuiltInCategory enumeration for exact names. " +
                    "Required for most searches to avoid scanning the whole database.", required: false)]
            public string Category { get; set; }

            [McpArg("The Revit API Class name (e.g., 'Wall', 'FamilyInstance', 'ViewSheet', 'Level'). " +
                    "Use this when searching for system classes that might not map cleanly to a single category, " +
                    "or to find specific API objects like 'ProjectInfo' or 'Material'. " +
                    "Can be used in conjunction with Category for precision.", required: false)]
            public string Class { get; set; }

            [McpArg("Filter by Element Name. Performs a case-insensitive partial match (contains). " +
                    "Useful for finding specific families by name (e.g., 'Single-Flush') or specific instances (e.g., 'Sheet A101').", required: false)]
            public string Name { get; set; }

            [McpArg("If true, retrieves Element Types (Symbols/Definitions) instead of instances. " +
                    "Use this when you need to find a 'WallType', 'FloorType', or 'FamilySymbol' to place a new element. " +
                    "If false (default), retrieves physical instances in the model.", required: false)]
            public bool GetTypes { get; set; } = false;

            [McpArg("The ElementId (long) of a specific View. " +
                    "If provided, the search is restricted ONLY to elements visible in this view. " +
                    "Critical for finding view-specific elements like Dimensions, Tags, or Text Notes. " +
                    "Note: This may trigger a view regeneration.", required: false)]
            public long? ViewId { get; set; }

            [McpArg("The ElementId (long) of a specific Level. " +
                    "If provided, filters elements associated with this level (e.g., Furniture on Level 1). " +
                    "Uses the ElementLevelFilter.", required: false)]
            public long? LevelId { get; set; }

            [McpArg("A dictionary of parameter names and values to filter by (e.g., {'Mark': '101', 'Reference Level': 'Level 1'}). " +
                    "Values are compared as strings (case-insensitive). This allows precise retrieval of specific elements based on their data.", required: false)]
            public Dictionary<string, string> ParameterFilters { get; set; }

            [McpArg("Limit the number of results returned to save bandwidth. Default is 50. " +
                    "Increase this only if you expect a large dataset, but be checking for performance.", required: false)]
            public int Limit { get; set; } = 50;
        }

        public McpResponse Execute(Autodesk.Revit.UI.UIApplication app, JObject jsonArgs)
        {
            Autodesk.Revit.DB.Document doc = app.ActiveUIDocument.Document;
            try
            {
                var args = jsonArgs.ToObject<Args>();

                // 1. CONSTRUCT COLLECTOR (Scope)
                // We use the constructor to limit scope immediately if a ViewId is provided.
                FilteredElementCollector collector;
                if (args.ViewId.HasValue)
                {
                    var viewId = new ElementId(args.ViewId.Value);
                    if (doc.GetElement(viewId) is not View)
                    {
                        return new McpResponse { Success = false, Message = $"Provided ViewId {args.ViewId} is not a valid View." };
                    }
                    collector = new FilteredElementCollector(doc, viewId);
                }
                else
                {
                    collector = new FilteredElementCollector(doc);
                }

                // 2. QUICK FILTERS (Native Optimization)
                // Always apply these first to narrow the memory expansion.

                // A. Type vs Instance (IsElementType / IsNotElementType)
                if (args.GetTypes)
                {
                    collector.WhereElementIsElementType();
                }
                else
                {
                    collector.WhereElementIsNotElementType();
                }

                // B. Category (Quick Filter)
                // Filters by BuiltInCategory. [cite_start]This is the primary way to narrow down the search space[cite: 66, 67].
                if (!string.IsNullOrEmpty(args.Category))
                {
                    if (Enum.TryParse(args.Category, true, out BuiltInCategory bic))
                    {
                        collector.OfCategory(bic);
                    }
                    else
                    {
                        return new McpResponse { Success = false, Message = $"Invalid BuiltInCategory: '{args.Category}'. Ensure it starts with 'OST_' (e.g., 'OST_Walls')." };
                    }
                }

                // C. Class (Quick Filter)
                // Filters by .NET Class type. [cite_start]Useful for finding system families or abstract classes[cite: 65, 66].
                if (!string.IsNullOrEmpty(args.Class))
                {
                    // Reflection to find the type in Revit API assembly
                    var assembly = typeof(Element).Assembly;
                    // Try "Autodesk.Revit.DB." prefix first, then bare name.
                    // This allows the AI to pass just "Wall" instead of "Autodesk.Revit.DB.Wall".
                    var type = assembly.GetType("Autodesk.Revit.DB." + args.Class) ?? assembly.GetType(args.Class);

                    if (type != null)
                    {
                        collector.OfClass(type);
                    }
                    else
                    {
                        return new McpResponse { Success = false, Message = $"Could not resolve Class: '{args.Class}' in Autodesk.Revit.DB namespace." };
                    }
                }

                // 3. SLOW FILTERS (Native)

                // D. Level Filter
                if (args.LevelId.HasValue)
                {
                    ElementId lvlId = new ElementId(args.LevelId.Value);
                    collector.WherePasses(new ElementLevelFilter(lvlId));
                }

                // 4. LINQ FILTERS (Managed Post-Processing)
                // Used for complex string matching or parameter values not easily done with ElementParameterFilter.

                IEnumerable<Element> query = collector;

                // E. Name Filter (Partial Match)
                // Case-insensitive "Contains" check.
                if (!string.IsNullOrEmpty(args.Name))
                {
                    query = query.Where(e => e.Name != null &&
                                             e.Name.IndexOf(args.Name, StringComparison.InvariantCultureIgnoreCase) >= 0);
                }

                // F. Parameter Filters
                // This checks the actual parameter values of the elements.
                // We use a custom helper method to handle various parameter storage types (String, Double, Integer, ElementId).
                if (args.ParameterFilters != null && args.ParameterFilters.Count > 0)
                {
                    query = query.Where(e => MatchesParameters(e, args.ParameterFilters));
                }

                // 5. EXECUTE & MAP
                // Materialize the query (fetch elements) and map to a simplified data object.
                var elements = query.Take(args.Limit).ToList();

                var data = elements.Select(e => new
                {
                    Name = e.Name,
                    Id = e.Id.Value,
                    UniqueId = e.UniqueId,
                    Category = e.Category?.Name ?? "Unknown",
                    Type = e.GetType().Name
                }).ToList();

                return new McpResponse
                {
                    Success = true,
                    Message = $"Found {data.Count} elements matching criteria.",
                    Data = data
                };
            }
            catch (Exception ex)
            {
                return new McpResponse { Success = false, Message = $"Error in GetElements: {ex.Message}" };
            }
        }

        // Helper method to check dictionary of parameters against an element
        private bool MatchesParameters(Element e, Dictionary<string, string> filters)
        {
            foreach (var filter in filters)
            {
                // Look up parameter by name.
                // Note: LookupParameter finds the *first* parameter with that name.
                var param = e.LookupParameter(filter.Key);
                if (param == null) return false;

                // Get value as string for comparison.
                // AsValueString() gets the user-visible string (with units).
                string paramValue = param.AsValueString();

                // If AsValueString() is null/empty (often happens for non-unit parameters or internal data),
                // fallback to the raw internal value.
                if (string.IsNullOrEmpty(paramValue))
                {
                    if (param.StorageType == StorageType.String) paramValue = param.AsString();
                    else if (param.StorageType == StorageType.Double) paramValue = param.AsDouble().ToString();
                    else if (param.StorageType == StorageType.Integer) paramValue = param.AsInteger().ToString();
                    else if (param.StorageType == StorageType.ElementId) paramValue = param.AsElementId().ToString();
                }

                if (paramValue == null) return false;

                // Case-insensitive comparison
                if (!paramValue.Equals(filter.Value, StringComparison.InvariantCultureIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }
    }
}