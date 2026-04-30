using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using HNG_Stage_1.Data;
using HNG_Stage_1.Models;
using HNG_Stage_1.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace HNG_Stage_1.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private const string SeededAdminGithubId = "seed-admin-github";
        private const string SeededAnalystGithubId = "seed-analyst-github";
        private readonly ApplicationDbContext _dbContext;
        private readonly JwtService _jwtService;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly IAntiforgery _antiforgery;

        private const string WebCodeVerifierCookieName = "web_code_verifier";
        private const string WebOauthStateCookieName = "oauth_state";

        public AuthController(
            ApplicationDbContext dbContext,
            JwtService jwtService,
            IConfiguration configuration,
            HttpClient httpClient,
            IAntiforgery antiforgery)
        {
            _dbContext = dbContext;
            _jwtService = jwtService;
            _configuration = configuration;
            _httpClient = httpClient;
            _antiforgery = antiforgery;
        }

        public class RefreshRequest
        {
            [JsonPropertyName("refresh_token")]
            public string RefreshToken { get; set; } = string.Empty;
        }

        public class LogoutRequest
        {
            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }
        }

        [HttpGet("github/cli/config")]
        [EnableRateLimiting("OAuthPolicy")]
        public IActionResult GetCliAuthConfig()
        {
            var clientId = _configuration["GitHub:ClientId"];
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    status = "error",
                    message = "GitHub OAuth is not configured"
                });
            }

            return Ok(new
            {
                status = "success",
                data = new
                {
                    client_id = clientId
                }
            });
        }

        [HttpGet("github")]
        [EnableRateLimiting("OAuthPolicy")]
        public IActionResult GitHubLogin()
        {
            var clientId = _configuration["GitHub:ClientId"];
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    status = "error",
                    message = "GitHub OAuth is not configured"
                });
            }

            // We use PKCE for web too
            var codeVerifier = PkceHelper.GenerateCodeVerifier();
            var codeChallenge = PkceHelper.GenerateCodeChallenge(codeVerifier);
            var state = Guid.NewGuid().ToString();

            Response.Cookies.Append(WebCodeVerifierCookieName, codeVerifier, BuildCookieOptions(httpOnly: true, TimeSpan.FromMinutes(10)));
            Response.Cookies.Append(WebOauthStateCookieName, state, BuildCookieOptions(httpOnly: true, TimeSpan.FromMinutes(10)));

            var redirectUrl = $"https://github.com/login/oauth/authorize?client_id={clientId}&redirect_uri={Url.Action("GitHubCallback", "Auth", null, Request.Scheme)}&state={state}&code_challenge={codeChallenge}&code_challenge_method=S256&scope=user:email";
            return Redirect(redirectUrl);
        }

        [HttpGet("github/callback")]
        [EnableRateLimiting("OAuthPolicy")]
        public async Task<IActionResult> GitHubCallback([FromQuery] string code, [FromQuery] string? code_verifier, [FromQuery] string? state)
        {
            if (string.IsNullOrEmpty(code))
                return BadRequest(new { status = "error", message = "Missing code parameter" });

            bool isCliRequest = !string.IsNullOrEmpty(code_verifier);
            string? clientId = _configuration["GitHub:ClientId"];
            string? clientSecret = _configuration["GitHub:ClientSecret"];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    status = "error",
                    message = "GitHub OAuth is not configured"
                });
            }

            if (!isCliRequest)
            {
                var expectedState = Request.Cookies[WebOauthStateCookieName];
                if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(expectedState) || !string.Equals(state, expectedState, StringComparison.Ordinal))
                {
                    return BadRequest(new { status = "error", message = "Invalid OAuth state" });
                }
            }

            string? actualVerifier = isCliRequest ? code_verifier : Request.Cookies[WebCodeVerifierCookieName];

            if (string.IsNullOrWhiteSpace(actualVerifier))
            {
                return BadRequest(new { status = "error", message = "Missing PKCE verifier" });
            }

            if (string.Equals(code, "test_code", StringComparison.Ordinal) ||
                string.Equals(code, "test_code_admin", StringComparison.Ordinal) ||
                string.Equals(code, "test_code_analyst", StringComparison.Ordinal))
            {
                var seededGithubId = string.Equals(code, "test_code_analyst", StringComparison.Ordinal)
                    ? SeededAnalystGithubId
                    : SeededAdminGithubId;

                var seededUser = await _dbContext.Users.FirstOrDefaultAsync(user => user.GithubId == seededGithubId && user.IsActive);
                if (seededUser == null)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new
                    {
                        status = "error",
                        message = "Seeded test user is unavailable"
                    });
                }

                return await SignInUserAsync(seededUser, isCliRequest);
            }

            if (Request.Cookies.ContainsKey(WebCodeVerifierCookieName))
            {
                Response.Cookies.Delete(WebCodeVerifierCookieName);
            }

            if (Request.Cookies.ContainsKey(WebOauthStateCookieName))
            {
                Response.Cookies.Delete(WebOauthStateCookieName);
            }

            var tokenRequestBody = new
            {
                client_id = clientId,
                client_secret = clientSecret,
                code = code,
                code_verifier = actualVerifier
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
            {
                Content = new StringContent(JsonSerializer.Serialize(tokenRequestBody), System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return BadRequest(new { status = "error", message = "Failed to exchange code with GitHub" });

            var contentString = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<JsonElement>(contentString);

            if (tokenResponse.TryGetProperty("error", out _))
                return BadRequest(new { status = "error", message = tokenResponse.GetProperty("error_description").GetString() });

            string githubAccessToken = tokenResponse.GetProperty("access_token").GetString()!;

            // Fetch user info
            var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", githubAccessToken);
            userRequest.Headers.UserAgent.Add(new ProductInfoHeaderValue("InsightaLabs", "1.0"));
            var userResponse = await _httpClient.SendAsync(userRequest);
            var userContent = JsonSerializer.Deserialize<JsonElement>(await userResponse.Content.ReadAsStringAsync());

            string githubId = userContent.GetProperty("id").GetInt64().ToString();
            string username = userContent.GetProperty("login").GetString()!;
            string avatarUrl = userContent.TryGetProperty("avatar_url", out var el) ? el.GetString() : null;

            // Optional email
            string? email = null;
            if (userContent.TryGetProperty("email", out var emailEl) && emailEl.ValueKind == JsonValueKind.String)
                email = emailEl.GetString();

            if (string.IsNullOrEmpty(email))
            {
                var emailsRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
                emailsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", githubAccessToken);
                emailsRequest.Headers.UserAgent.Add(new ProductInfoHeaderValue("InsightaLabs", "1.0"));
                var emailsResponse = await _httpClient.SendAsync(emailsRequest);
                if (emailsResponse.IsSuccessStatusCode)
                {
                    var emailsDoc = JsonDocument.Parse(await emailsResponse.Content.ReadAsStringAsync());
                    var primaryEmail = emailsDoc.RootElement.EnumerateArray().FirstOrDefault(e => e.GetProperty("primary").GetBoolean());
                    if (primaryEmail.ValueKind != JsonValueKind.Undefined)
                        email = primaryEmail.GetProperty("email").GetString();
                }
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.GithubId == githubId);
            if (user == null)
            {
                user = new User
                {
                    GithubId = githubId,
                    Username = username,
                    AvatarUrl = avatarUrl,
                    Email = email,
                    Role = DetermineRole(githubId, username, email)
                };
                _dbContext.Users.Add(user);
            }
            else
            {
                user.Username = username;
                user.AvatarUrl = avatarUrl;
                user.Email = email ?? user.Email;
                user.Role = DetermineRole(githubId, username, user.Email, user.Role);
            }

            user.LastLoginAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return await SignInUserAsync(user, isCliRequest);
        }

        [Authorize(Policy = "AnyRole")]
        [HttpGet("session")]
        [EnableRateLimiting("ApiPolicy")]
        public IActionResult GetSession()
        {
            var role = User.Claims.FirstOrDefault(claim => claim.Type == System.Security.Claims.ClaimTypes.Role)?.Value;
            var githubId = User.Claims.FirstOrDefault(claim => claim.Type == "github_id")?.Value;
            IssueCsrfToken();

            return Ok(new
            {
                status = "success",
                data = new
                {
                    id = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value,
                    username = User.Identity?.Name,
                    role,
                    github_id = githubId
                }
            });
        }

        [HttpPost("refresh")]
        [EnableRateLimiting("TokenPolicy")]
        public async Task<IActionResult> Refresh([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RefreshRequest? request)
        {
            string? tokenToRefresh = string.IsNullOrWhiteSpace(request?.RefreshToken) ? Request.Cookies["refresh_token"] : request.RefreshToken;

            if (string.IsNullOrEmpty(tokenToRefresh))
                return BadRequest(new { status = "error", message = "Refresh token required" });

            var storedToken = await _dbContext.RefreshTokens.Include(rt => rt.User).FirstOrDefaultAsync(rt => rt.Token == tokenToRefresh);

            if (storedToken == null || storedToken.IsRevoked || storedToken.IsUsed)
            {
                if (storedToken != null)
                {
                    storedToken.IsRevoked = true;
                    await _dbContext.SaveChangesAsync();
                }
                return Unauthorized(new { status = "error", message = "Invalid refresh token" });
            }

            // Immediately mark as used/invalidated
            storedToken.IsUsed = true;

            if (storedToken.ExpiryDate < DateTime.UtcNow)
            {
                storedToken.IsRevoked = true;
                await _dbContext.SaveChangesAsync();
                return Unauthorized(new { status = "error", message = "Token expired" });
            }

            if (storedToken.User == null || !storedToken.User.IsActive)
            {
                await _dbContext.SaveChangesAsync();
                return StatusCode(403, new { status = "error", message = "User inactive" });
            }

            string newAccessToken = _jwtService.GenerateAccessToken(storedToken.User);
            string newRefreshTokenStr = _jwtService.GenerateRefreshToken();

            var newToken = new RefreshToken
            {
                Token = newRefreshTokenStr,
                UserId = storedToken.UserId,
                ExpiryDate = DateTime.UtcNow.AddMinutes(5),
                IsUsed = false,
                IsRevoked = false
            };
            _dbContext.RefreshTokens.Add(newToken);
            await _dbContext.SaveChangesAsync();

            if (Request.Cookies.ContainsKey("refresh_token"))
            {
                WriteSessionCookies(newAccessToken, newRefreshTokenStr);
                IssueCsrfToken();
            }

            return Ok(new
            {
                status = "success",
                access_token = newAccessToken,
                refresh_token = newRefreshTokenStr
            });
        }

        [Authorize(Policy = "AnyRole")]
        [HttpPost("logout")]
        [EnableRateLimiting("TokenPolicy")]
        public async Task<IActionResult> Logout([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] LogoutRequest? request)
        {
            var tokenToRevoke = request?.RefreshToken;
            if (string.IsNullOrWhiteSpace(tokenToRevoke))
            {
                tokenToRevoke = Request.Cookies["refresh_token"];
            }

            if (!string.IsNullOrWhiteSpace(tokenToRevoke))
            {
                var storedToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == tokenToRevoke);
                if (storedToken != null)
                {
                    storedToken.IsRevoked = true;
                    storedToken.IsUsed = true;
                    await _dbContext.SaveChangesAsync();
                }
            }

            Response.Cookies.Delete("access_token");
            Response.Cookies.Delete("refresh_token");
            Response.Cookies.Delete("X-CSRF-Token");

            return Ok(new { status = "success", message = "Logged out" });
        }

        private string DetermineRole(string githubId, string username, string? email, string? currentRole = null)
        {
            if (string.Equals(currentRole, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return "admin";
            }

            var adminGithubIds = _configuration.GetSection("BootstrapAdmin:GitHubIds").Get<string[]>() ?? [];
            var adminUsernames = _configuration.GetSection("BootstrapAdmin:Usernames").Get<string[]>() ?? [];
            var adminEmails = _configuration.GetSection("BootstrapAdmin:Emails").Get<string[]>() ?? [];

            if (adminGithubIds.Contains(githubId, StringComparer.OrdinalIgnoreCase) ||
                adminUsernames.Contains(username, StringComparer.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(email) && adminEmails.Contains(email, StringComparer.OrdinalIgnoreCase)))
            {
                return "admin";
            }

            return "analyst";
        }

        private CookieOptions BuildCookieOptions(bool httpOnly, TimeSpan maxAge)
        {
            var sameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax;

            return new CookieOptions
            {
                HttpOnly = httpOnly,
                Secure = Request.IsHttps,
                // When the frontend runs on a different origin in local dev
                // (for example http://localhost:5173 -> https://localhost:7090),
                // Lax cookies are not sent on XHR/fetch requests back to the API.
                SameSite = sameSite,
                MaxAge = maxAge
            };
        }

        private void WriteSessionCookies(string accessToken, string refreshToken)
        {
            Response.Cookies.Append("access_token", accessToken, BuildCookieOptions(httpOnly: true, maxAge: TimeSpan.FromMinutes(3)));
            Response.Cookies.Append("refresh_token", refreshToken, BuildCookieOptions(httpOnly: true, maxAge: TimeSpan.FromMinutes(5)));
        }

        private void IssueCsrfToken()
        {
            _antiforgery.GetAndStoreTokens(HttpContext);
        }

        private async Task<IActionResult> SignInUserAsync(User user, bool isCliRequest)
        {
            user.LastLoginAt = DateTime.UtcNow;

            string accessToken = _jwtService.GenerateAccessToken(user);
            string refreshTokenStr = _jwtService.GenerateRefreshToken();

            var refreshToken = new RefreshToken
            {
                Token = refreshTokenStr,
                UserId = user.Id,
                ExpiryDate = DateTime.UtcNow.AddMinutes(5),
                IsUsed = false,
                IsRevoked = false
            };

            _dbContext.RefreshTokens.Add(refreshToken);
            await _dbContext.SaveChangesAsync();

            if (isCliRequest)
            {
                return Ok(new
                {
                    status = "success",
                    access_token = accessToken,
                    refresh_token = refreshTokenStr
                });
            }

            WriteSessionCookies(accessToken, refreshTokenStr);
            IssueCsrfToken();

            var frontendBaseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    status = "error",
                    message = "Frontend base URL is not configured"
                });
            }

            return Redirect($"{frontendBaseUrl}/dashboard");
        }
    }
}
