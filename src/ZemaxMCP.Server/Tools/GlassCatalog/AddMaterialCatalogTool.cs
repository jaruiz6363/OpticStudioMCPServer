using System.ComponentModel;
using ModelContextProtocol.Server;
using ZemaxMCP.Core.Session;

namespace ZemaxMCP.Server.Tools.GlassCatalog;

[McpServerToolType]
public class AddMaterialCatalogTool(IZemaxSession session)
{
    private readonly IZemaxSession _session = session;

    public record AddMaterialCatalogResult(
        bool Success,
        string? Error,
        string CatalogName,
        bool AlreadyInUse,
        List<string>? CatalogsInUse
    );

    [McpServerTool(Name = "zemax_add_material_catalog")]
    [Description("Add a glass/material catalog to the current project's list of catalogs in use (System Explorer > Material Catalogs)")]
    public async Task<AddMaterialCatalogResult> ExecuteAsync(
        [Description("Catalog name without .agf extension (e.g. 'SCHOTT', 'OHARA', 'MISC')")] string catalogName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(catalogName))
                return new AddMaterialCatalogResult(false, "Catalog name cannot be empty", catalogName ?? "", false, null);

            return await _session.ExecuteAsync("AddMaterialCatalog",
                new Dictionary<string, object?> { ["catalogName"] = catalogName },
                system =>
            {
                var catalogs = system.SystemData.MaterialCatalogs;
                var trimmed = catalogName.Trim();

                if (catalogs.IsCatalogInUse(trimmed))
                {
                    return new AddMaterialCatalogResult(
                        Success: true,
                        Error: null,
                        CatalogName: trimmed,
                        AlreadyInUse: true,
                        CatalogsInUse: catalogs.GetCatalogsInUse().ToList()
                    );
                }

                var available = catalogs.GetAvailableCatalogs();
                if (!available.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                {
                    return new AddMaterialCatalogResult(
                        Success: false,
                        Error: $"Catalog '{trimmed}' is not available in the Zemax Glasscat directory. Available: {string.Join(", ", available)}",
                        CatalogName: trimmed,
                        AlreadyInUse: false,
                        CatalogsInUse: catalogs.GetCatalogsInUse().ToList()
                    );
                }

                bool added = catalogs.AddCatalog(trimmed);
                if (!added)
                {
                    return new AddMaterialCatalogResult(
                        Success: false,
                        Error: $"Failed to add catalog '{trimmed}' to the project",
                        CatalogName: trimmed,
                        AlreadyInUse: false,
                        CatalogsInUse: catalogs.GetCatalogsInUse().ToList()
                    );
                }

                return new AddMaterialCatalogResult(
                    Success: true,
                    Error: null,
                    CatalogName: trimmed,
                    AlreadyInUse: false,
                    CatalogsInUse: catalogs.GetCatalogsInUse().ToList()
                );
            });
        }
        catch (Exception ex)
        {
            return new AddMaterialCatalogResult(false, ex.Message, catalogName, false, null);
        }
    }
}
