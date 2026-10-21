using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Base;
namespace Tybim.Revit.Upstream
{
    public static class UpstreamCatalog
    {
        public static readonly IReadOnlyDictionary<string, Func<UIApplication, ExternalEventCommandBase>> Factories = new Dictionary<string, Func<UIApplication, ExternalEventCommandBase>>
        {
            { "ai_element_filter", app => new RevitMCPCommandSet.Commands.AIElementFilterCommand(app) },
            { "analyze_model_statistics", app => new RevitMCPCommandSet.Commands.DataExtraction.AnalyzeModelStatisticsCommand(app) },
            { "color_splash", app => new RevitMCPCommandSet.Commands.ColorSplashCommand(app) },
            { "create_dimensions", app => new RevitMCPCommandSet.Commands.AnnotationComponents.CreateDimensionCommand(app) },
            { "create_grid", app => new RevitMCPCommandSet.Commands.CreateGridCommand(app) },
            { "create_level", app => new RevitMCPCommandSet.Commands.Architecture.CreateLevelCommand(app) },
            { "create_line_based_element", app => new RevitMCPCommandSet.Commands.CreateLineElementCommand(app) },
            { "create_point_based_element", app => new RevitMCPCommandSet.Commands.CreatePointElementCommand(app) },
            { "create_room", app => new RevitMCPCommandSet.Commands.Architecture.CreateRoomCommand(app) },
            { "create_structural_framing_system", app => new RevitMCPCommandSet.Commands.CreateStructuralFramingSystemCommand(app) },
            { "create_surface_based_element", app => new RevitMCPCommandSet.Commands.CreateSurfaceElementCommand(app) },
            { "delete_element", app => new RevitMCPCommandSet.Commands.Delete.DeleteElementCommand(app) },
            { "export_room_data", app => new RevitMCPCommandSet.Commands.DataExtraction.ExportRoomDataCommand(app) },
            { "get_available_family_types", app => new RevitMCPCommandSet.Commands.Access.GetAvailableFamilyTypesCommand(app) },
            { "get_current_view_elements", app => new RevitMCPCommandSet.Commands.Access.GetCurrentViewElementsCommand(app) },
            { "get_current_view_info", app => new RevitMCPCommandSet.Commands.Access.GetCurrentViewInfoCommand(app) },
            { "get_material_quantities", app => new RevitMCPCommandSet.Commands.DataExtraction.GetMaterialQuantitiesCommand(app) },
            { "get_selected_elements", app => new RevitMCPCommandSet.Commands.Access.GetSelectedElementsCommand(app) },
            { "operate_element", app => new RevitMCPCommandSet.Commands.OperateElementCommand(app) },
            { "say_hello", app => new RevitMCPCommandSet.Commands.Test.SayHelloCommand(app) },
            { "send_code_to_revit", app => new RevitMCPCommandSet.Commands.ExecuteDynamicCode.ExecuteCodeCommand(app) },
            { "tag_rooms", app => new RevitMCPCommandSet.Commands.TagRoomsCommand(app) },
            { "tag_walls", app => new RevitMCPCommandSet.Commands.TagWallsCommand(app) },
        };
    }
}
