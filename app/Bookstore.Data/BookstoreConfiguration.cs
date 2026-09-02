using Microsoft.Extensions.Configuration;

namespace BobsBookstoreClassic.Data
{
    public sealed class BookstoreConfiguration
    {
        private static readonly Dictionary<string, string> _appSettings = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _connectionStrings = new Dictionary<string, string>();

        /// <summary>
        /// Initializes configuration from ASP.NET Core IConfiguration.
        /// Must be called at application startup before any GetSetting calls.
        /// </summary>
        public static void Initialize(IConfiguration configuration)
        {
            // Load all configuration values, converting ":" delimiters to "/" for backward compatibility
            foreach (var kvp in configuration.AsEnumerable())
            {
                if (kvp.Value != null)
                {
                    var key = kvp.Key.Replace(":", "/");
                    _appSettings[key] = kvp.Value;

                    // Also check environment variables with the same key
                    var envValue = Environment.GetEnvironmentVariable(kvp.Key);
                    if (envValue != null)
                    {
                        _appSettings[key] = envValue;
                    }
                }
            }

            // Load connection strings
            var connectionStrings = configuration.GetSection("ConnectionStrings");
            foreach (var cs in connectionStrings.GetChildren())
            {
                _connectionStrings[cs.Key] = cs.Value ?? string.Empty;
            }
        }

        public static void AddSetting(string key, string value)
        {
            _appSettings[key] = value;
        }

        public static string GetSetting(string key)
        {
            if (_appSettings.TryGetValue(key, out var value))
                return value;

            return string.Empty;
        }

        public static T GetSetting<T>(string key)
        {
            var value = GetSetting(key);
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static void AddConnectionString(string key, string value)
        {
            _connectionStrings[key] = value;
        }

        public static string GetConnectionString(string key)
        {
            if (_connectionStrings.TryGetValue(key, out var value))
                return value;

            return string.Empty;
        }
    }
}
