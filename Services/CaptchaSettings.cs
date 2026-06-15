using Microsoft.Extensions.Configuration;

namespace MyProject12.Services
{
    public static class CaptchaSettings
    {
        public static bool IsEnabled(IConfiguration configuration, string key)
        {
            var value = configuration[$"googleReCaptcha:{key}"];
            return !string.Equals(value?.Trim(), "off", StringComparison.OrdinalIgnoreCase);
        }
    }
}
