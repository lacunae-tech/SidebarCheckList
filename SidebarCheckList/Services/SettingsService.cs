using SidebarChecklist.Models;
using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SidebarChecklist.Services
{
    public sealed class SettingsService
    {
        private readonly string _path;

        public SettingsService(string appDir)
        {
            _path = Path.Combine(appDir, "settings.json");
        }

        public SettingsRoot LoadRequiredOrThrow()
        {
            if (!File.Exists(_path))
                throw new InvalidOperationException("settings.json missing");

            try
            {
                var json = File.ReadAllText(_path);
                var obj = JsonSerializer.Deserialize<SettingsRoot>(json, JsonOptions());
                if (obj is null) throw new InvalidOperationException("settings.json invalid");

                // 読めた場合の丸め/フォールバックは呼び出し側で実施
                return obj;
            }
            catch
            {
                throw new InvalidOperationException("settings.json invalid");
            }
        }

        public void Save(SettingsRoot settings)
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions(writeIndented: true));
            File.WriteAllText(_path, json);
        }

        private static JsonSerializerOptions JsonOptions(bool writeIndented = false)
            => new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = writeIndented
            };
    }
}
