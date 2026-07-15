namespace Financial.Models.ViewModels
{
    public class ExistingFileDetails
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
