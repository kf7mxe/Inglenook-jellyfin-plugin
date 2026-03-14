using Jellyfin.Plugin.Inglenook.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Inglenook.Api;

/// <summary>
/// Controller that serves the Inglenook SPA web client.
/// </summary>
[ApiController]
[Route("Inglenook/App")]
[AllowAnonymous]
public class SpaController : ControllerBase
{
    private readonly SpaManager _spaManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpaController"/> class.
    /// </summary>
    public SpaController(SpaManager spaManager)
    {
        _spaManager = spaManager;
    }

    /// <summary>
    /// Gets the SPA status including version and availability.
    /// </summary>
    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetStatus()
    {
        var config = Plugin.Instance?.Configuration;
        return Ok(new
        {
            Enabled = config?.EnableSpaServing ?? true,
            Available = _spaManager.IsAvailable,
            Version = _spaManager.GetCurrentVersion(),
            LastUpdateCheck = config?.LastSpaUpdateCheck ?? string.Empty,
            AutoUpdateEnabled = config?.EnableSpaAutoUpdate ?? true,
            GitHubRepo = config?.SpaGitHubRepo ?? string.Empty
        });
    }

    /// <summary>
    /// Serves the SPA files. Falls back to index.html for client-side routes.
    /// </summary>
    [HttpGet("{**path}")]
    public ActionResult ServeSpa([FromRoute] string? path = null)
    {
        if (Plugin.Instance?.Configuration?.EnableSpaServing != true)
        {
            return Content(
                """
                <!DOCTYPE html>
                <html>
                <head><title>Inglenook</title>
                <style>
                    body { font-family: system-ui, sans-serif; display: flex; justify-content: center;
                           align-items: center; height: 100vh; margin: 0; background: #1a1a2e; color: #e0e0e0; }
                    .container { text-align: center; }
                </style>
                </head>
                <body>
                    <div class="container">
                        <h2>Inglenook Web Client is Disabled</h2>
                        <p>The web client has been disabled in the plugin settings.</p>
                    </div>
                </body>
                </html>
                """,
                "text/html");
        }

        if (!_spaManager.IsAvailable)
        {
            return Content(
                """
                <!DOCTYPE html>
                <html>
                <head><title>Inglenook</title>
                <meta http-equiv="refresh" content="5">
                <style>
                    body { font-family: system-ui, sans-serif; display: flex; justify-content: center;
                           align-items: center; height: 100vh; margin: 0; background: #1a1a2e; color: #e0e0e0; }
                    .container { text-align: center; }
                    .spinner { width: 40px; height: 40px; border: 4px solid #333; border-top-color: #6c63ff;
                               border-radius: 50%; animation: spin 1s linear infinite; margin: 20px auto; }
                    @keyframes spin { to { transform: rotate(360deg); } }
                </style>
                </head>
                <body>
                    <div class="container">
                        <div class="spinner"></div>
                        <h2>Inglenook is being downloaded...</h2>
                        <p>This page will refresh automatically. Please wait a moment.</p>
                    </div>
                </body>
                </html>
                """,
                "text/html");
        }

        var filePath = _spaManager.GetFilePath(path);
        if (filePath is null)
        {
            return BadRequest();
        }

        if (System.IO.File.Exists(filePath))
        {
            var extension = Path.GetExtension(filePath);
            var mimeType = SpaManager.GetMimeType(extension);
            return PhysicalFile(filePath, mimeType);
        }

        // SPA fallback: if the path has no extension, it's likely a client-side route
        if (string.IsNullOrEmpty(Path.GetExtension(filePath)))
        {
            var indexPath = Path.Combine(_spaManager.SpaWebRoot, "index.html");
            if (System.IO.File.Exists(indexPath))
            {
                return PhysicalFile(indexPath, "text/html");
            }
        }

        return NotFound();
    }
}
