using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using Serilog;
using ZemaxMCP.Core.Logging;
using ZemaxMCP.Core.Services.ConstrainedOptimization;
using ZemaxMCP.Core.Session;
using ZemaxMCP.Documentation;
using ZemaxMCP.Server.Hosting;

// Redirect Console.Out to prevent ZOSAPI (or any library) from polluting stdout.
// MCP stdio transport uses the raw process stdout stream, not Console.Out.
Console.SetOut(TextWriter.Null);

// Initialize ZOSAPI assembly resolver BEFORE any ZOSAPI types are loaded
ZOSAPI_NetHelper.ZOSAPI_Initializer.Initialize();

// Configure Serilog - write to file only (console interferes with stdio)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File("logs/zemaxmcp-.log", rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting ZemaxMCP Server");

    // Use CreateEmptyApplicationBuilder for stdio transport (avoids console output)
    var builder = Host.CreateEmptyApplicationBuilder(settings: null);

    // Add Serilog
    builder.Services.AddSerilog();

    // Add configuration
    builder.Services.Configure<ZemaxConnectionOptions>(options =>
    {
        options.Mode = ConnectionMode.Standalone;
        options.TimeoutSeconds = 30;
    });

    // Add command logging - creates a dedicated log file for all ZEMAX commands
    builder.Services.AddSingleton<IZemaxCommandLog>(sp =>
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        return new ZemaxCommandLog(logDir);
    });

    // Add core services
    builder.Services.AddSingleton<IZemaxSession, ZemaxSession>();
    builder.Services.AddSingleton<OperandDatabase>();
    builder.Services.AddSingleton<OperandSearchService>();
    builder.Services.AddSingleton<ConstraintStore>();
    builder.Services.AddSingleton<MultistartState>();

    // Add MCP server with stdio transport
    builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "zemax-mcp",
            Version = "1.0.0"
        };
    })
    .WithStdioServerTransport()
    // Analysis Tools
    .WithTools<ZemaxMCP.Server.Tools.Analysis.SpotDiagramTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.MtfAnalysisTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.GeometricMtfTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.RayTraceTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.RmsSpotTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.CardinalPointsTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.SeidelCoefficientsTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.LateralColorTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.LongitudinalAberrationTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.ChromaticFocalShiftTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.FieldCurvatureDistortionTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.RayFanTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.OpticalPathFanTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.PupilAberrationFanTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.FftMtfVsFieldTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.DiffractionEncircledEnergyTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.GeometricEncircledEnergyTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.GeometricMtfVsFieldTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.RelativeIlluminationTool>()
    .WithTools<ZemaxMCP.Server.Tools.Analysis.CrossSectionLayoutTool>()
    // Optimization Tools
    .WithTools<ZemaxMCP.Server.Tools.Optimization.GetMeritFunctionTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.AddOperandTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.RemoveOperandTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.OptimizeTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.OperandHelpTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.SearchOperandsTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.OptimizationWizardTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.HammerOptimizationTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.GlobalSearchTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.SaveMeritFunctionFileTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.LoadMeritFunctionFileTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.ForbesMeritFunctionTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.GetVariablesTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.SetVariableConstraintsTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.ConstrainedOptimizeTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.MultistartOptimizeTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.MultistartStatusTool>()
    .WithTools<ZemaxMCP.Server.Tools.Optimization.MultistartStopTool>()
    // Lens Data Tools
    .WithTools<ZemaxMCP.Server.Tools.LensData.GetSystemDataTool>()
    .WithTools<ZemaxMCP.Server.Tools.LensData.GetSurfaceTool>()
    .WithTools<ZemaxMCP.Server.Tools.LensData.SetSurfaceTool>()
    .WithTools<ZemaxMCP.Server.Tools.LensData.AddSurfaceTool>()
    .WithTools<ZemaxMCP.Server.Tools.LensData.SetFieldsTool>()
    .WithTools<ZemaxMCP.Server.Tools.LensData.SetWavelengthsTool>()
    .WithTools<ZemaxMCP.Server.Tools.LensData.SetApertureTool>()
    .WithTools<ZemaxMCP.Server.Tools.LensData.GetSurfaceSolvesTool>()
    .WithTools<ZemaxMCP.Server.Tools.LensData.SetSurfaceSolveTool>()
    .WithTools<ZemaxMCP.Server.Tools.LensData.GetAsphericSurfaceTool>()
    .WithTools<ZemaxMCP.Server.Tools.LensData.SetAsphericSurfaceTool>()
    // Configuration Tools
    .WithTools<ZemaxMCP.Server.Tools.Configuration.GetConfigurationTool>()
    .WithTools<ZemaxMCP.Server.Tools.Configuration.SetNumberOfConfigurationsTool>()
    .WithTools<ZemaxMCP.Server.Tools.Configuration.SetCurrentConfigurationTool>()
    .WithTools<ZemaxMCP.Server.Tools.Configuration.AddConfigurationOperandTool>()
    .WithTools<ZemaxMCP.Server.Tools.Configuration.DeleteConfigurationOperandTool>()
    .WithTools<ZemaxMCP.Server.Tools.Configuration.GetConfigurationOperandsTool>()
    .WithTools<ZemaxMCP.Server.Tools.Configuration.SetConfigurationOperandValueTool>()
    // System Tools
    .WithTools<ZemaxMCP.Server.Tools.System.OpenFileTool>()
    .WithTools<ZemaxMCP.Server.Tools.System.SaveFileTool>()
    .WithTools<ZemaxMCP.Server.Tools.System.NewSystemTool>()
    .WithTools<ZemaxMCP.Server.Tools.System.ConnectTool>()
    // System Settings Tools
    .WithTools<ZemaxMCP.Server.Tools.SystemSettings.GetRayAimingTool>()
    .WithTools<ZemaxMCP.Server.Tools.SystemSettings.SetRayAimingTool>()
    .WithTools<ZemaxMCP.Server.Tools.SystemSettings.GetAfocalModeTool>()
    .WithTools<ZemaxMCP.Server.Tools.SystemSettings.SetAfocalModeTool>()
    // Glass Catalog Tools
    .WithTools<ZemaxMCP.Server.Tools.GlassCatalog.GetGlassCatalogsTool>()
    .WithTools<ZemaxMCP.Server.Tools.GlassCatalog.GetGlassesTool>()
    .WithTools<ZemaxMCP.Server.Tools.GlassCatalog.FilterGlassesTool>()
    .WithTools<ZemaxMCP.Server.Tools.GlassCatalog.ExportGlassCatalogTool>()
    .WithTools<ZemaxMCP.Server.Tools.GlassCatalog.AddMaterialCatalogTool>()
    // Resources
    .WithResources<ZemaxMCP.Server.Resources.CurrentSystemResource>()
    .WithResources<ZemaxMCP.Server.Resources.MeritFunctionResource>()
    .WithResources<ZemaxMCP.Server.Resources.OperandDocumentationResource>()
    // Prompts
    .WithPrompts<ZemaxMCP.Server.Prompts.DesignPrompts>()
    .WithPrompts<ZemaxMCP.Server.Prompts.OptimizationPrompts>()
    .WithPrompts<ZemaxMCP.Server.Prompts.AnalysisPrompts>();

    var host = builder.Build();

    // Log the command log file location
    var commandLog = host.Services.GetRequiredService<IZemaxCommandLog>();
    Log.Information("ZEMAX Command Log: {LogPath}", commandLog.LogFilePath);
    Log.Information("MCP Server configured, starting...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
