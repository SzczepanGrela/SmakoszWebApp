using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.API.Controllers;

[Route("api/config")]
[ApiController]
public class ConfigController : ControllerBase
{
    private readonly IPublicConfigProvider _configProvider;

    public ConfigController(IPublicConfigProvider configProvider)
    {
        _configProvider = configProvider;
    }

    [AllowAnonymous]
    [HttpGet("public")]
    [ResponseCache(Duration = 300)]
    public async Task<IActionResult> GetPublicConfig(CancellationToken ct)
    {
        var config = await _configProvider.GetPublicConfigAsync(ct);
        return Ok(config);
    }
}
