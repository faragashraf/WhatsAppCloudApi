using Microsoft.AspNetCore.Mvc;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ToActionResult<T>(ApiResponse<T> response)
    {
        if (response.Success)
        {
            return Ok(response);
        }

        var statusCode = response.Error?.StatusCode ?? StatusCodes.Status500InternalServerError;
        return StatusCode(statusCode, response);
    }
}
