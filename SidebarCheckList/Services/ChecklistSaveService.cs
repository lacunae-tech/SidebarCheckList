using SidebarChecklist.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SidebarChecklist.Services
{
    public sealed class ChecklistSaveService
    {
        private readonly string _saveDir;

        public ChecklistSaveService(string appDir, string? configuredDir = null)
        {
            _saveDir = ResolveSaveDirectory(appDir, configuredDir);
        }

        public string Save(ChecklistSaveEntry entry)
        {
            if (entry is null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            Directory.CreateDirectory(_saveDir);

            var fileName = $"{DateTime.Now:yyyy-MM-dd}.json";
            var path = Path.Combine(_saveDir, fileName);
            var entries = LoadEntries(path);
            entries.Add(entry);

            var json = JsonSerializer.Serialize(entries, JsonOptions());
            File.WriteAllText(path, json);
            return path;
        }

        private static List<ChecklistSaveEntry> LoadEntries(string path)
        {
            if (!File.Exists(path))
            {
                return new List<ChecklistSaveEntry>();
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<ChecklistSaveEntry>>(json, JsonOptions())
                    ?? new List<ChecklistSaveEntry>();
            }
            catch
            {
                return new List<ChecklistSaveEntry>();
            }
        }

        private static string ResolveSaveDirectory(string appDir, string? configuredDir)
        {
            if (string.IsNullOrWhiteSpace(configuredDir))
            {
                return appDir;
            }

            var trimmed = configuredDir.Trim();
            var candidate = Path.IsPathRooted(trimmed) ? trimmed : Path.Combine(appDir, trimmed);

            if (candidate.EndsWith(Path.DirectorySeparatorChar) || candidate.EndsWith(Path.AltDirectorySeparatorChar))
            {
                return candidate;
            }

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var extension = Path.GetExtension(candidate);
            if (string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(candidate) ?? appDir;
            }

            return candidate;
        }

        private static JsonSerializerOptions JsonOptions()
            => new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
    }
}
