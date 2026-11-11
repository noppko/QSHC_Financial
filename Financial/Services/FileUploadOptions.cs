namespace Financial.Services
{
    /// <summary>
    /// การตั้งค่า FileUpload จาก appsettings.json — ปลายทางกำหนดที่ server เท่านั้น ไม่รับจาก browser
    /// </summary>
    public class FileUploadOptions
    {
        public const string SECTION_NAME = "FileUpload";

        public long MaxFileSizeInBytes { get; set; } = 50 * 1024 * 1024;
        public string DefaultDestinationPath { get; set; } = string.Empty;
        public string[] AllowedExtensions { get; set; } = [];

        public bool IsAllowedExtension(string fileName)
        {
            var ext = Path.GetExtension(fileName);
            return !string.IsNullOrEmpty(ext)
                && (AllowedExtensions.Length == 0 || AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// ป้องกัน Path Traversal: รับเฉพาะ "ชื่อไฟล์" และต้องอยู่ภายใต้โฟลเดอร์ปลายทางที่กำหนด
    /// </summary>
    public static class UploadPathGuard
    {
        public static bool TryResolve(string rootDirectory, string? fileName, out string fullPath)
        {
            fullPath = string.Empty;

            if (string.IsNullOrWhiteSpace(rootDirectory) || string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            // ปฏิเสธชื่อที่มี path/ไดรฟ์ปนมา เช่น "..\x.pdf", "C:\x.pdf", "sub/x.pdf"
            var nameOnly = Path.GetFileName(fileName);
            if (!string.Equals(nameOnly, fileName, StringComparison.Ordinal)
                || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(':')
                || nameOnly is "." or ".."
                || nameOnly.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return false;
            }

            var root = Path.GetFullPath(rootDirectory);
            var candidate = Path.GetFullPath(Path.Combine(root, nameOnly));
            var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

            if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }
    }
}
