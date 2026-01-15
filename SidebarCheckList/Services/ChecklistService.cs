using SidebarChecklist.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public ChecklistService(string appDir)
        {
            _path = Path.Combine(appDir, "checklist.json");
        }

        public ChecklistLoadResult LoadOptional()
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
