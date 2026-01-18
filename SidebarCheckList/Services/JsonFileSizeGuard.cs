using System;
using System.IO;
using System.Text;

namespace SidebarChecklist.Services
{
    public sealed class JsonFileSizeExceededException : Exception
    {
        public bool IsWriteOperation { get; }

        public JsonFileSizeExceededException(string message, bool isWriteOperation)
            : base(message)
        {
            IsWriteOperation = isWriteOperation;
        }
    }

    public static class JsonFileSizeGuard
    {
        public const long MaxJsonBytes = 5 * 1024 * 1024;

        public static void EnsureFileWithinLimit(string path)
        {
            var info = new FileInfo(path);
            if (info.Exists && info.Length > MaxJsonBytes)
            {
                throw new JsonFileSizeExceededException("json file too large", isWriteOperation: false);
            }
        }

        public static void EnsureJsonWithinLimit(string json)
        {
            if (IsJsonTooLarge(json))
            {
                throw new JsonFileSizeExceededException("json content too large", isWriteOperation: true);
            }
        }

        public static bool IsJsonTooLarge(string json)
            => Encoding.UTF8.GetByteCount(json) > MaxJsonBytes;
    }
}
