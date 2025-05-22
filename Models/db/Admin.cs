using System.ComponentModel.DataAnnotations;

namespace AttendEase.Models.db
{
    public class Admin
    {
        [Key]
        public int AdminId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }

        public ICollection<Branch> Branches { get; set; }
    }

}
