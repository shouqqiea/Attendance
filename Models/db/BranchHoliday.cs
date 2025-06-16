using System.ComponentModel.DataAnnotations;

namespace LetsCheckIn.Models.db
{
    public class BranchHoliday
    {
        [Key]
        public int HolidayId { get; set; }

        public int BranchId { get; set; }
        public Branch Branch { get; set; }

        public DateTime Date { get; set; }
        public string Name { get; set; }
    }

}
