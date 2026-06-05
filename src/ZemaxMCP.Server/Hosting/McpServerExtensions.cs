using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using ZemaxMCP.Server.Tools.Analysis;
using ZemaxMCP.Server.Tools.Optimization;
using ZemaxMCP.Server.Tools.LensData;
using ZemaxMCP.Server.Tools.System;
using ZemaxMCP.Server.Tools.Configuration;
using ZemaxMCP.Server.Tools.SystemSettings;
using ZemaxMCP.Server.Resources;
using ZemaxMCP.Server.Prompts;

namespace ZemaxMCP.Server.Hosting;

public static class McpServerExtensions
{
    public static IServiceCollection AddZemaxMcpServer(this IServiceCollection services)
    {
        services.AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "zemax-mcp",
                Version = "1.0.0"
            };
        })
        // Analysis Tools
        .WithTools<SpotDiagramTool>()
        .WithTools<MtfAnalysisTool>()
        .WithTools<RayTraceTool>()
        .WithTools<RmsSpotTool>()
        .WithTools<CardinalPointsTool>()

        // Optimization Tools
        .WithTools<GetMeritFunctionTool>()
        .WithTools<AddOperandTool>()
        .WithTools<RemoveOperandTool>()
        .WithTools<OptimizeTool>()
        .WithTools<OperandHelpTool>()
        .WithTools<SearchOperandsTool>()

        // Lens Data Tools
        .WithTools<GetSystemDataTool>()
        .WithTools<GetSurfaceTool>()
        .WithTools<SetSurfaceTool>()
        .WithTools<AddSurfaceTool>()
        .WithTools<SetFieldsTool>()
        .WithTools<SetWavelengthsTool>()
        .WithTools<SetApertureTool>()
        .WithTools<GetAsphericSurfaceTool>()
        .WithTools<SetAsphericSurfaceTool>()

        // System Tools
        .WithTools<OpenFileTool>()
        .WithTools<SaveFileTool>()
        .WithTools<NewSystemTool>()
        .WithTools<ConnectTool>()

        // Configuration Tools
        .WithTools<GetConfigurationTool>()
        .WithTools<SetNumberOfConfigurationsTool>()
        .WithTools<SetCurrentConfigurationTool>()
        .WithTools<AddConfigurationOperandTool>()
        .WithTools<DeleteConfigurationOperandTool>()

        // System Settings Tools
        .WithTools<GetRayAimingTool>()
        .WithTools<SetRayAimingTool>()
        .WithTools<GetAfocalModeTool>()
        .WithTools<SetAfocalModeTool>()
        .WithTools<GetApodizationTool>()
        .WithTools<SetApodizationTool>()
        .WithTools<GetClearSemiDiameterMarginTool>()
        .WithTools<SetClearSemiDiameterMarginTool>()

        // Resources
        .WithResources<CurrentSystemResource>()
        .WithResources<MeritFunctionResource>()
        .WithResources<OperandDocumentationResource>()

        // Prompts
        .WithPrompts<DesignPrompts>()
        .WithPrompts<OptimizationPrompts>()
        .WithPrompts<AnalysisPrompts>();

        return services;
    }
}
