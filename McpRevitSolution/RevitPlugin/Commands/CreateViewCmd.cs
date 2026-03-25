using System;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitPlugin.Attributes;
using RevitPlugin.Core;

namespace RevitPlugin.Commands
{
    [McpTool("CreateView", "Creates a basic view like a Floor Plan or 3D View.", requiresTransaction: true)]
    public class CreateViewCmd : IRevitCommand
    {
        public class Args
        {
            [McpArg("Type of view to create (e.g., 'FloorPlan', '3D').", required: true)]
            public string ViewType { get; set; }

            [McpArg("Name of the new view.", required: true)]
            public string Name { get; set; }

            [McpArg("Level ElementId. Required for FloorPlan and CeilingPlan views.", required: false)]
            public long? LevelId { get; set; }
        }

        public McpResponse Execute(Autodesk.Revit.UI.UIApplication app, JObject jsonArgs)
        {
            Autodesk.Revit.DB.Document doc = app.ActiveUIDocument.Document;
            var args = jsonArgs.ToObject<Args>();
            ElementId newViewId = ElementId.InvalidElementId;

            if (args.ViewType.Equals("FloorPlan", StringComparison.OrdinalIgnoreCase))
            {
                if (!args.LevelId.HasValue)
                {
                    return new McpResponse { Success = false, Message = "LevelId is required for FloorPlan views." };
                }

                ElementId levelId = new ElementId(args.LevelId.Value);
                if (!(doc.GetElement(levelId) is Level level))
                {
                    return new McpResponse { Success = false, Message = $"Level with ID {args.LevelId.Value} not found." };
                }

                // Find a FloorPlan ViewFamilyType
                ViewFamilyType viewFamilyType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(x => x.ViewFamily == ViewFamily.FloorPlan);

                if (viewFamilyType == null)
                {
                    return new McpResponse { Success = false, Message = "No FloorPlan ViewFamilyType found in the document." };
                }

                ViewPlan viewPlan = ViewPlan.Create(doc, viewFamilyType.Id, level.Id);
                viewPlan.Name = args.Name;
                newViewId = viewPlan.Id;
            }
            else if (args.ViewType.Equals("3D", StringComparison.OrdinalIgnoreCase))
            {
                // Find a 3D ViewFamilyType
                ViewFamilyType viewFamilyType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                if (viewFamilyType == null)
                {
                    return new McpResponse { Success = false, Message = "No 3D ViewFamilyType found in the document." };
                }

                View3D view3D = View3D.CreateIsometric(doc, viewFamilyType.Id);
                view3D.Name = args.Name;
                newViewId = view3D.Id;
            }
            else
            {
                return new McpResponse { Success = false, Message = $"Unsupported ViewType: {args.ViewType}. Use 'FloorPlan' or '3D'." };
            }

            return new McpResponse
            {
                Success = true,
                Message = $"Created {args.ViewType} view '{args.Name}'.",
                Data = new { ElementId = newViewId.Value }
            };
        }
    }
}
