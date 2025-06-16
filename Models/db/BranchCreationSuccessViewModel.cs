namespace LetsCheckIn.Models.db
{
    public class BranchCreationSuccessViewModel
    {
        public string BranchName { get; set; } = string.Empty;
        public string AdminName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public bool EnableLocation { get; set; }
        public bool EnableNetwork { get; set; }
        public bool EnableBiometric { get; set; }
    }
}
