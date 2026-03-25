using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitPlugin.Attributes;
using RevitPlugin.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitPlugin.Commands
{
    [McpTool("GetElementParameters", 
        "Retrieves specific parameter values from a single Revit element. " +
        "Use this AFTER you have found an Element's ID using 'GetElements'. " +
        "SCENARIOS:\n" +
        "1. Inspect Data: Get the 'Unconnected Height' or 'Fire Rating' of a specific Wall.\n" +
        "2. Debugging: Check the value of a parameter to see why a filter failed.\n" +
        "3. Units: Returns values as formatted strings (e.g., '1200 mm') as seen in the UI.")]
    public class GetElementParametersCmd : IRevitCommand
    {
        public class Args
        {
            [McpArg("The ElementId (long) of the element to inspect.", required: true)]
            public long ElementId { get; set; }

            [McpArg("A list of Parameter Names to retrieve (e.g., ['Mark', 'Comments', 'Level']). " +
                    "If left empty/null, the tool attempts to retrieve ALL visible parameters (Use with caution on large elements).", required: false)]
            public List<string> ParameterNames { get; set; }
        }

        public McpResponse Execute(Autodesk.Revit.UI.UIApplication app, JObject jsonArgs)
        {
            Autodesk.Revit.DB.Document doc = app.ActiveUIDocument.Document;
            try
            {
                var args = jsonArgs.ToObject<Args>();
                var elementId = new ElementId(args.ElementId);
                var element = doc.GetElement(elementId);

                if (element == null)
                {
                    return new McpResponse { Success = false, Message = $"Element with ID {args.ElementId} not found." };
                }

                var resultData = new Dictionary<string, string>();

                // Scenario A: User requested specific parameters (Fast & Precise)
                if (args.ParameterNames != null && args.ParameterNames.Count > 0)
                {
                    foreach (var paramName in args.ParameterNames)
                    {
                        var param = element.LookupParameter(paramName);
                        resultData[paramName] = GetParameterValue(param); // Returns "null" string if not found
                    }
                }
                // Scenario B: User wants everything (Slower, but good for exploration)
                else
                {
                    foreach (Parameter param in element.Parameters)
                    {
                        if (param.Definition != null)
                        {
                            // Avoid duplicate keys if multiple params have same name (rare but possible)
                            if (!resultData.ContainsKey(param.Definition.Name))
                            {
                                resultData[param.Definition.Name] = GetParameterValue(param);
                            }
                        }
                    }
                }

                return new McpResponse
                {
                    Success = true,
                    Message = $"Retrieved {resultData.Count} parameters for element '{element.Name}' (ID: {element.Id}).",
                    Data = resultData
                };
            }
            catch (Exception ex)
            {
                return new McpResponse { Success = false, Message = $"Error getting parameters: {ex.Message}" };
            }
        }

        private string GetParameterValue(Parameter param)
        {
            if (param == null) return " <null> "; // Distinct marker for missing param

            // 1. Try to get the string as seen in the Revit Properties Palette
            string value = param.AsValueString();

            // 2. If AsValueString is empty (common for strings, bools, or internal data), read raw storage
            if (string.IsNullOrEmpty(value))
            {
                switch (param.StorageType)
                {
                    case StorageType.String:
                        value = param.AsString();
                        break;
                    case StorageType.Integer:
                        value = param.AsInteger().ToString();
                        break;
                    case StorageType.Double:
                        value = param.AsDouble().ToString("F4"); // Raw internal units if no formatting exists
                        break;
                    case StorageType.ElementId:
                        var id = param.AsElementId();
                        value = (id == ElementId.InvalidElementId) ? "<None>" : id.Value.ToString();
                        break;
                }
            }

            return value ?? ""; // Return empty string if absolutely nothing found
        }
    }
}