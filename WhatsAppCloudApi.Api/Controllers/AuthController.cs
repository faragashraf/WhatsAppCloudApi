using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/auth")]
#if DEBUG
[Microsoft.AspNetCore.Mvc.ApiExplorerSettings(IgnoreApi = false)]
#endif
public sealed class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    // Development-only token issuer for testing. Returns a JWT signed with Jwt:Key.
    [HttpPost("token")]
    public IActionResult GetToken()
    {
        var key = _config["Jwt:Key"];
        if (string.IsNullOrEmpty(key)) return BadRequest("Jwt key is not configured");

        var issuer = _config["Jwt:Issuer"];
        var audience = _config["Jwt:Audience"];

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "test-user"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new { token = tokenString });
    }
}
