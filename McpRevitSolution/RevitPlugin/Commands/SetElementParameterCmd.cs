using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitPlugin.Attributes;
using RevitPlugin.Core;
using System;
using System.Collections.Generic;

namespace RevitPlugin.Commands
{
    [McpTool("SetElementParameter",
        "Modifies the value of a parameter on a specific element. " +
        "Use this to update properties like 'Mark', 'Comments', or 'Offset'. " +
        "SCENARIOS:\n" +
        "1. Text Updates: Set 'Comments' to 'Reviewed'.\n" +
        "2. Numeric Updates: Set 'Unconnected Height' to 3000.0 (Ensure units match internal Revit units, usually feet).\n" +
        "3. ID Updates: Set 'Level' by providing the ElementId of the new level.\n" +
        "WARNINGS:\n" +
        "- Check if the parameter is Read-Only (formulas) before setting.\n" +
        "- Ensure the Value data type matches the parameter (don't send text to a number field).",
        requiresTransaction: true)]
    public class SetElementParameterCmd : IRevitCommand
    {
        public class Args
        {
            [McpArg("The Integer ID of the element to modify.", required: true)]
            public long ElementId { get; set; }

            [McpArg("The exact Name of the parameter to set (e.g., 'Comments', 'Mark', 'Base Offset').", required: true)]
            public string ParameterName { get; set; }

            [McpArg("The new value. Can be a String, Number (Double/Int), or ElementId (Long). " +
                    "The tool will attempt to convert this to the correct storage type automatically.", required: true)]
            public object Value { get; set; }
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
                    return new McpResponse { Success = false, Message = $"Element {args.ElementId} not found." };
                }

                // 1. FIND PARAMETER
                var param = element.LookupParameter(args.ParameterName);
                if (param == null)
                {
                    return new McpResponse { Success = false, Message = $"Parameter '{args.ParameterName}' not found on element {element.Name}." };
                }

                // 2. CHECK READ-ONLY STATUS
                if (param.IsReadOnly)
                {
                    return new McpResponse { Success = false, Message = $"Parameter '{args.ParameterName}' is Read-Only (controlled by formula or system)." };
                }

                // 3. SET VALUE BASED ON STORAGE TYPE (Transaction handled by Dispatcher)
                bool success = false;
                string message = "";

                switch (param.StorageType)
                {
                    case StorageType.String:
                        string strVal = args.Value.ToString();
                        success = param.Set(strVal);
                        message = $"Set String to '{strVal}'";
                        break;

                    case StorageType.Double:
                        if (double.TryParse(args.Value.ToString(), out double dVal))
                        {
                            success = param.Set(dVal);
                            message = $"Set Double to {dVal}";
                        }
                        else
                        {
                            message = $"Failed to convert '{args.Value}' to Double.";
                        }
                        break;

                    case StorageType.Integer:
                        if (int.TryParse(args.Value.ToString(), out int iVal))
                        {
                            success = param.Set(iVal);
                            message = $"Set Integer to {iVal}";
                        }
                        else
                        {
                            // Handle Booleans (True/False sent as strings)
                            string boolStr = args.Value.ToString().ToLower();
                            if (boolStr == "true") { success = param.Set(1); message = "Set Boolean to True"; }
                            else if (boolStr == "false") { success = param.Set(0); message = "Set Boolean to False"; }
                            else { message = $"Failed to convert '{args.Value}' to Integer."; }
                        }
                        break;

                    case StorageType.ElementId:
                        if (long.TryParse(args.Value.ToString(), out long idVal))
                        {
                            success = param.Set(new ElementId(idVal));
                            message = $"Set ElementId to {idVal}";
                        }
                        else
                        {
                            message = $"Failed to convert '{args.Value}' to ElementId.";
                        }
                        break;

                    default:
                        message = "Unsupported parameter storage type.";
                        break;
                }

                if (success)
                {
                    return new McpResponse { Success = true, Message = $"Successfully updated '{args.ParameterName}': {message}." };
                }
                else
                {
                    return new McpResponse { Success = false, Message = $"Failed to set parameter. Revit internal error or type mismatch. Debug info: {message}" };
                }
            }
            catch (Exception ex)
            {
                return new McpResponse { Success = false, Message = $"Critical Error: {ex.Message}" };
            }
        }
    }
}