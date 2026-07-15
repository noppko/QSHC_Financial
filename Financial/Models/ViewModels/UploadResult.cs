namespace Financial.Models.ViewModels
{
    public class UploadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadTime { get; set; }
        public bool FileExists { get; set; }
        public ExistingFileDetails? ExistingFileInfo { get; set; }
    }
}
