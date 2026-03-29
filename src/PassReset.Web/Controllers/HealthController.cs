using Microsoft.AspNetCore.Mvc;

namespace PassReset.Web.Controllers;

/// <summary>
/// Provides a lightweight health probe for load balancers and monitoring.
/// GET /api/health
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Returns the application health status.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get() =>
        Ok(new
        {
            status    = "healthy",
            timestamp = DateTimeOffset.UtcNow,
        });
}
