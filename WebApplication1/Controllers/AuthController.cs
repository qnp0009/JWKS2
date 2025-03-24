using Microsoft.AspNetCore.Mvc;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly KeyManager _keyManager;

    public AuthController(KeyManager keyManager)
    {
        _keyManager = keyManager;
    }

    [HttpPost]
    public IActionResult IssueToken([FromQuery] bool expired = false)
    {
        try
        {
            // Get the appropriate key (expired or valid)
            var key = expired ? _keyManager.GetExpiredKey() : _keyManager.GetValidKeys().FirstOrDefault();
            if (key == null)
            {
                return BadRequest("No valid or expired key available.");
            }

            // Define claims for the JWT
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "user"),
                new Claim(ClaimTypes.Role, "admin")
            };

            // Create signing credentials using the RSA key
            var credentials = new SigningCredentials(new RsaSecurityKey(key.RsaKey), SecurityAlgorithms.RsaSha256);

            // Create the JWT header with the Key ID (kid)
            var header = new JwtHeader(credentials)
            {
                { "kid", key.Kid } // Include the Key ID in the JWT header
            };

            // Create the JWT payload
            var payload = new JwtPayload(
                issuer: "JwksServer",
                audience: "client",
                claims: claims,
                notBefore: null,
                expires: expired ? key.Expiry : DateTime.UtcNow.AddHours(1),
                issuedAt: DateTime.UtcNow
            );

            // Create the JWT
            var token = new JwtSecurityToken(header, payload);

            // Write the JWT to a string
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwt = tokenHandler.WriteToken(token);
            return Ok(new { token = jwt });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error issuing token: {ex.Message}");
            return StatusCode(500, "An error occurred while issuing the token.");
        }
    }
}