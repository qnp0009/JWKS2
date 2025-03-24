using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Linq;

[ApiController]
[Route(".well-known")]
public class JwksController : ControllerBase
{
    private readonly KeyManager _keyManager;

    public JwksController(KeyManager keyManager)
    {
        _keyManager = keyManager;
    }

    [HttpGet("jwks.json")]
    public IActionResult GetJwks()
    {
        try
        {
            // Get all valid (non-expired) keys
            var validKeys = _keyManager.GetValidKeys();

            // Format the keys in JWKS format
            var jwks = new
            {
                keys = validKeys.Select(k => new
                {
                    kid = k.Kid, // Key ID
                    kty = "RSA", // Key type
                    alg = "RS256", // Algorithm
                    use = "sig", // Intended use (signature)
                    n = Base64UrlEncoder.Encode(k.RsaKey.ExportParameters(false).Modulus), // Modulus
                    e = Base64UrlEncoder.Encode(k.RsaKey.ExportParameters(false).Exponent) // Exponent
                })
            };

            return Ok(jwks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving JWKS: {ex.Message}");
            return StatusCode(500, "An error occurred while retrieving JWKS.");
        }
    }
}