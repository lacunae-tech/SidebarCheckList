using SidebarChecklist.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;

namespace SidebarChecklist.Services
{
    public sealed class ChecklistLoadResult
    {
        public ChecklistRoot? Root { get; init; }
        public string? ErrorMessage { get; init; } // 本体領域に表示する文言
    }

    public sealed class ChecklistService
    {
        private readonly string _path;
        private readonly string _cachePath;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromHours(12);

        public ChecklistService(string appDir)
        {
            _path = Path.Combine(appDir, "checklist.json");
            _cachePath = Path.Combine(appDir, "checklist.cache.json");
        }

        public ChecklistLoadResult LoadOptional(SettingsRoot settings)
        {
            if (settings.Api.Enabled && !string.IsNullOrWhiteSpace(settings.Api.BaseUrl))
            {
                var apiResult = LoadFromApi(settings.Api);
                if (apiResult.Root is not null)
                {
                    return apiResult;
                }

                var cacheResult = LoadFromCache();
                if (cacheResult.Root is not null)
                {
                    return cacheResult;
                }

                return apiResult.ErrorMessage is null ? cacheResult : apiResult;
            }

            return LoadFromFile();
        }

        private ChecklistLoadResult LoadFromFile()
        {
            if (!File.Exists(_path))
            {
                return new ChecklistLoadResult
                {
                    Root = null,
                    ErrorMessage = "チェックリストが存在しません"
                };
            }

            ChecklistRoot? root;
            try
            {
                var json = File.ReadAllText(_path);
                return ParseChecklist(json);
            }
            catch
            {
                return new ChecklistLoadResult
                {
                    Root = null,
                    ErrorMessage = "JSONファイルエラー"
                };
            }
        }

        private ChecklistLoadResult LoadFromApi(ApiSettings api)
        {
            try
            {
                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromMilliseconds(Math.Max(api.TimeoutMs, 1))
                };

                if (!string.IsNullOrWhiteSpace(api.ApiKey))
                {
                    client.DefaultRequestHeaders.Add("X-API-Key", api.ApiKey);
                }

                var response = client.GetAsync(api.BaseUrl).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    return new ChecklistLoadResult
                    {
                        Root = null,
                        ErrorMessage = "チェックリストが存在しません"
                    };
                }

                var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var parsed = ParseChecklist(json);
                if (parsed.Root is not null)
                {
                    SaveCache(json);
                }

                return parsed;
            }
            catch
            {
                return new ChecklistLoadResult
                {
                    Root = null,
                    ErrorMessage = "チェックリストが存在しません"
                };
            }
        }

        private ChecklistLoadResult LoadFromCache()
        {
            if (!File.Exists(_cachePath))
            {
                return new ChecklistLoadResult
                {
                    Root = null,
                    ErrorMessage = "チェックリストが存在しません"
                };
            }

            var lastWriteUtc = File.GetLastWriteTimeUtc(_cachePath);
            if (DateTime.UtcNow - lastWriteUtc > _cacheTtl)
            {
                return new ChecklistLoadResult
                {
                    Root = null,
                    ErrorMessage = "チェックリストが存在しません"
                };
            }

            try
            {
                var json = File.ReadAllText(_cachePath);
                return ParseChecklist(json);
            }
            catch
            {
                return new ChecklistLoadResult
                {
                    Root = null,
                    ErrorMessage = "JSONファイルエラー"
                };
            }
        }

        private void SaveCache(string json)
        {
            try
            {
                File.WriteAllText(_cachePath, json);
            }
            catch
            {
                // cache保存失敗は無視
            }
        }

        private ChecklistLoadResult ParseChecklist(string json)
        {
            ChecklistRoot? root;
            try
            {
                root = JsonSerializer.Deserialize<ChecklistRoot>(json, JsonOptions());
                if (root is null) throw new Exception("invalid");

                // 境界条件
                if (root.Lists is null || root.Lists.Count == 0)
                {
                    return new ChecklistLoadResult
                    {
                        Root = null,
                        ErrorMessage = "チェックリストが存在しません"
                    };
                }

                // id重複 → JSONファイルエラー
                var dup = root.Lists
                    .Where(l => !string.IsNullOrWhiteSpace(l.Id))
                    .GroupBy(l => l.Id)
                    .Any(g => g.Count() >= 2);

                if (dup)
                {
                    return new ChecklistLoadResult
                    {
                        Root = null,
                        ErrorMessage = "JSONファイルエラー"
                    };
                }

                // items空は許容（空表示）
                foreach (var l in root.Lists)
                {
                    l.Items ??= new List<string>();
                    l.Id ??= "";
                    l.Name ??= "";
                }

                return new ChecklistLoadResult { Root = root, ErrorMessage = null };
            }
            catch
            {
                return new ChecklistLoadResult
                {
                    Root = null,
                    ErrorMessage = "JSONファイルエラー"
                };
            }
        }

        private static JsonSerializerOptions JsonOptions()
            => new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
    }
}
