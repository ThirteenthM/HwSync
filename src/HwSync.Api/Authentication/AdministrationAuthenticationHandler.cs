using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HwSync.Api.Authentication
{
    /// <summary>
    /// Проверяет персональный ключ пользователя управления, отдельно от участников синхронизации.
    /// </summary>
    public sealed class AdministrationAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Administration";
        public const string ReadPolicy = "AdministrationRead";
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Принимает конфигурацию пользователей и службы аутентификации.
        /// </summary>
        public AdministrationAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder, IConfiguration configuration)
            : base(options, logger, encoder)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Сравнивает хеш ключа с разрешёнными пользователями, не сохраняя сам ключ.
        /// </summary>
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string authorization = Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            string token = authorization[7..];
            if (token.Length is < 32 or > 512 || token.Any(char.IsWhiteSpace))
            {
                return Task.FromResult(AuthenticateResult.Fail("Некорректный ключ доступа."));
            }

            byte[] actual = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            foreach (IConfigurationSection user in _configuration.GetSection("Administration:Users").GetChildren())
            {
                string? hash = user["TokenSha256"];
                string? name = user["Name"];
                if (string.IsNullOrWhiteSpace(name) || hash is null || hash.Length != 64 || !hash.All(Uri.IsHexDigit))
                {
                    continue;
                }

                if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(hash)))
                {
                    continue;
                }

                List<Claim> claims = [new(ClaimTypes.Name, name)];
                if (bool.TryParse(user["CanRead"], out bool canRead) && canRead)
                {
                    claims.Add(new("administration", "read"));
                }

                ClaimsIdentity identity = new(claims, SchemeName);
                return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), SchemeName)));
            }

            return Task.FromResult(AuthenticateResult.Fail("Неизвестный ключ доступа."));
        }
    }
}
