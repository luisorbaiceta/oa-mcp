using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitPlugin.Attributes;
using RevitPlugin.Core;

namespace RevitPlugin.Commands
{
    [McpTool("ApplyViewTemplate", "Applies an existing View Template to a specific View.", requiresTransaction: true)]
    public class ApplyViewTemplateCmd : IRevitCommand
    {
        public class Args
        {
            [McpArg("The ElementId of the View.", required: true)]
            public long ViewId { get; set; }

            [McpArg("The ElementId of the View Template to apply.", required: true)]
            public long TemplateId { get; set; }
        }

        public McpResponse Execute(Autodesk.Revit.UI.UIApplication app, JObject jsonArgs)
        {
            Autodesk.Revit.DB.Document doc = app.ActiveUIDocument.Document;
            var args = jsonArgs.ToObject<Args>();

            ElementId viewId = new ElementId(args.ViewId);
            if (!(doc.GetElement(viewId) is View view))
            {
                return new McpResponse { Success = false, Message = $"View with ID {args.ViewId} not found or is not a View." };
            }

            ElementId templateId = new ElementId(args.TemplateId);
            if (!(doc.GetElement(templateId) is View templateView) || !templateView.IsTemplate)
            {
                return new McpResponse { Success = false, Message = $"View Template with ID {args.TemplateId} not found or is not a View Template." };
            }

            if (!view.IsValidViewTemplate(templateId))
            {
                return new McpResponse { Success = false, Message = $"View Template {templateView.Name} cannot be applied to View {view.Name}." };
            }

            view.ViewTemplateId = templateId;

            return new McpResponse
            {
                Success = true,
                Message = $"Applied template '{templateView.Name}' to view '{view.Name}'.",
                Data = new { ViewId = view.Id.Value, TemplateId = templateId.Value }
            };
        }
    }
}
