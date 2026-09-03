using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace BobsBookstoreClassic.Data
{
    public sealed class BookstoreConfiguration
    {
        private static readonly Lazy<BookstoreConfiguration> Lazy = new Lazy<BookstoreConfiguration>(() => new BookstoreConfiguration());

        private static BookstoreConfiguration Instance => Lazy.Value;

        private readonly Dictionary<string, string> _appSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _connectionStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private BookstoreConfiguration()
        {
        }

        /// <summary>
        /// Initializes BookstoreConfiguration from an IConfiguration instance.
        /// Flattens all configuration keys, converting the ":" separator used by IConfiguration
        /// to the "/" separator used throughout this application.
        /// </summary>
        public static void Initialize(IConfiguration configuration)
        {
            // Load all flat settings (converted from ":" to "/")
            foreach (var kvp in configuration.AsEnumerable())
            {
                if (kvp.Value != null)
                {
                    var normalizedKey = kvp.Key.Replace(":", "/");
                    Instance._appSettings[normalizedKey] = kvp.Value;
                }
            }

            // Load connection strings
            var connStrSection = configuration.GetSection("ConnectionStrings");
            foreach (var cs in connStrSection.GetChildren())
            {
                Instance._connectionStrings[cs.Key] = cs.Value ?? string.Empty;
            }

            // Environment variable overrides (using normalized "/" keys)
            foreach (var key in new List<string>(Instance._appSettings.Keys))
            {
                var envKey = key.Replace("/", "__");
                var envValue = Environment.GetEnvironmentVariable(envKey);
                if (envValue != null)
                {
                    Instance._appSettings[key] = envValue;
                }
            }
        }

        public static void AddSetting(string key, string value)
        {
            Instance._appSettings[key] = value;
        }

        public static string? GetSetting(string key)
        {
            if (Instance._appSettings.TryGetValue(key, out var value))
                return value;
            return null;
        }

        public static T? GetSetting<T>(string key)
        {
            var value = GetSetting(key);
            if (value == null) return default;
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static void AddConnectionString(string key, string value)
        {
            Instance._connectionStrings[key] = value;
        }

        public static string? GetConnectionString(string key)
        {
            if (Instance._connectionStrings.TryGetValue(key, out var value))
                return value;
            return null;
        }
    }
}
