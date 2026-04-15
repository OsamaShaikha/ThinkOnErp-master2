using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.API.Middleware;

/// <summary>
/// Middleware that checks if a user has been force logged out.
/// Validates that the JWT token was issued after the user's FORCE_LOGOUT_DATE.
/// </summary>
public class ForceLogoutMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ForceLogoutMiddleware> _logger;

    public ForceLogoutMiddleware(
        RequestDelegate next,
        ILogger<ForceLogoutMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IUserRepository userRepository)
    {
        // Only check authenticated requests
        if (context.User.Identity?.IsAuthenticated == true)
        {
            try
            {
                // Extract user ID from claims
                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == "userId");
                if (userIdClaim != null && long.TryParse(userIdClaim.Value, out long userId))
                {
                    // Get user from database to check force logout date
                    var user = await userRepository.GetByIdAsync(userId);
                    
                    if (user != null && user.ForceLogoutDate.HasValue)
                    {
                        // Extract token issued time from JWT
                        var tokenIssuedAt = GetTokenIssuedTime(context);
                        
                        if (tokenIssuedAt.HasValue && tokenIssuedAt.Value < user.ForceLogoutDate.Value)
                        {
                            _logger.LogWarning(
                                "User {UserId} attempted to use token issued at {TokenIssuedAt} but was force logged out at {ForceLogoutDate}",
                                userId,
                                tokenIssuedAt.Value,
                                user.ForceLogoutDate.Value);

                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            context.Response.ContentType = "application/json";
                            
                            var response = new
                            {
                                success = false,
                                statusCode = 401,
                                message = "Your session has been terminated by an administrator. Please login again.",
                                timestamp = DateTime.UtcNow.ToString("o")
                            };

                            await context.Response.WriteAsJsonAsync(response);
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking force logout status");
                // Don't block the request if there's an error checking force logout
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Extracts the token issued time (iat claim) from the JWT token.
    /// </summary>
    private DateTime? GetTokenIssuedTime(HttpContext context)
    {
        try
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return null;
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            var handler = new JwtSecurityTokenHandler();
            
            if (!handler.CanReadToken(token))
            {
                return null;
            }

            var jwtToken = handler.ReadJwtToken(token);
            
            // Get the 'iat' (issued at) claim
            var iatClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "iat");
            if (iatClaim != null && long.TryParse(iatClaim.Value, out long iatUnix))
            {
                // Convert Unix timestamp to DateTime
                return DateTimeOffset.FromUnixTimeSeconds(iatUnix).UtcDateTime;
            }

            // If no 'iat' claim, use 'nbf' (not before) as fallback
            var nbfClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "nbf");
            if (nbfClaim != null && long.TryParse(nbfClaim.Value, out long nbfUnix))
            {
                return DateTimeOffset.FromUnixTimeSeconds(nbfUnix).UtcDateTime;
            }

            // If neither claim exists, use current time minus token lifetime as approximation
            if (jwtToken.ValidTo != DateTime.MinValue)
            {
                var tokenLifetime = jwtToken.ValidTo - DateTime.UtcNow;
                return DateTime.UtcNow - tokenLifetime;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting token issued time");
            return null;
        }
    }
}
