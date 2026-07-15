namespace Financial.Models.ViewModels
{
    public class UserProfile
    {
        public int Id { get; set; }
        public string LoginName { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Identifier { get; set; } = null!;
        public string Gender { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string JobPosition { get; set; } = null!;
        public string RoleName { get; set; } = null!;
    }
}
