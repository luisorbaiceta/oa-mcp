using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitPlugin.Attributes;
using RevitPlugin.Core;

namespace RevitPlugin.Commands
{
    [McpTool("PlaceViewOnSheet", "Places an existing View onto an existing Sheet.", requiresTransaction: true)]
    public class PlaceViewOnSheetCmd : IRevitCommand
    {
        public class Args
        {
            [McpArg("The ElementId of the Sheet.", required: true)]
            public long SheetId { get; set; }

            [McpArg("The ElementId of the View to place.", required: true)]
            public long ViewId { get; set; }
        }

        public McpResponse Execute(Autodesk.Revit.UI.UIApplication app, JObject jsonArgs)
        {
            Autodesk.Revit.DB.Document doc = app.ActiveUIDocument.Document;
            var args = jsonArgs.ToObject<Args>();

            ElementId sheetId = new ElementId(args.SheetId);
            if (!(doc.GetElement(sheetId) is ViewSheet sheet))
            {
                return new McpResponse { Success = false, Message = $"Sheet with ID {args.SheetId} not found or is not a Sheet." };
            }

            ElementId viewId = new ElementId(args.ViewId);
            if (!(doc.GetElement(viewId) is View view))
            {
                return new McpResponse { Success = false, Message = $"View with ID {args.ViewId} not found or is not a View." };
            }

            if (!Viewport.CanAddViewToSheet(doc, sheetId, viewId))
            {
                return new McpResponse { Success = false, Message = $"View '{view.Name}' cannot be placed on Sheet '{sheet.SheetNumber}'." };
            }

            // Calculate center of the sheet for placement
            BoundingBoxUV outline = sheet.Outline;
            XYZ center = new XYZ((outline.Max.U + outline.Min.U) / 2.0, (outline.Max.V + outline.Min.V) / 2.0, 0);

            Viewport viewport = Viewport.Create(doc, sheetId, viewId, center);

            return new McpResponse
            {
                Success = true,
                Message = $"Placed view '{view.Name}' on sheet '{sheet.SheetNumber}'.",
                Data = new { ViewportId = viewport.Id.Value }
            };
        }
    }
}
