using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitPlugin.Attributes;
using RevitPlugin.Core;

namespace RevitPlugin.Commands
{
    [McpTool("CreateLevel", "Creates a new Level at the specified elevation.", requiresTransaction: true)]
    public class CreateLevelCmd : IRevitCommand
    {
        public class Args
        {
            [McpArg("Name of the level", required: true)]
            public string Name { get; set; }

            [McpArg("Elevation in meters", required: true)]
            public double Elevation { get; set; }
        }

        public McpResponse Execute(Autodesk.Revit.UI.UIApplication app, JObject jsonArgs)
        {
            Autodesk.Revit.DB.Document doc = app.ActiveUIDocument.Document;
            var args = jsonArgs.ToObject<Args>();

            // Default Revit internal unit for length is Feet.
            // Convert Meters to Feet.
            // Using UnitUtils in 2025: UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Meters)
            
            double elevationInFeet = UnitUtils.ConvertToInternalUnits(args.Elevation, UnitTypeId.Meters);

            // Check if level at elevation exists? Not strictly necessary, allows duplicates.
            
            Level level = Level.Create(doc, elevationInFeet);
            level.Name = args.Name;

            return new McpResponse
            {
                Success = true,
                Message = $"Created Level '{level.Name}' at {args.Elevation}m.",
                Data = new { ElementId = level.Id.Value, Elevation = args.Elevation }
            };
        }
    }
}
