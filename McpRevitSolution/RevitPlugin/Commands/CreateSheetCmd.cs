using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitPlugin.Attributes;
using RevitPlugin.Core;

namespace RevitPlugin.Commands
{
    [McpTool("CreateSheet", "Creates a new drawing sheet.", requiresTransaction: true)]
    public class CreateSheetCmd : IRevitCommand
    {
        public class Args
        {
            [McpArg("Sheet Number (e.g., 'A101').", required: true)]
            public string SheetNumber { get; set; }

            [McpArg("Sheet Name (e.g., 'Ground Floor Plan').", required: true)]
            public string SheetName { get; set; }

            [McpArg("The ElementId of the TitleBlock family symbol to use. If omitted, uses a default placeholder if available.", required: false)]
            public long? TitleBlockId { get; set; }
        }

        public McpResponse Execute(Autodesk.Revit.UI.UIApplication app, JObject jsonArgs)
        {
            Autodesk.Revit.DB.Document doc = app.ActiveUIDocument.Document;
            var args = jsonArgs.ToObject<Args>();

            ElementId titleBlockId = ElementId.InvalidElementId;

            if (args.TitleBlockId.HasValue)
            {
                titleBlockId = new ElementId(args.TitleBlockId.Value);
            }
            else
            {
                // Try to find a default title block
                var titleBlockSymbol = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsElementType()
                    .FirstOrDefault();
                
                if (titleBlockSymbol != null)
                {
                    titleBlockId = titleBlockSymbol.Id;
                }
            }

            ViewSheet sheet = ViewSheet.Create(doc, titleBlockId);
            sheet.SheetNumber = args.SheetNumber;
            sheet.Name = args.SheetName;

            return new McpResponse
            {
                Success = true,
                Message = $"Created Sheet '{args.SheetNumber} - {args.SheetName}'.",
                Data = new { ElementId = sheet.Id.Value }
            };
        }
    }
}
