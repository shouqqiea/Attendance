using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace LetsCheckIn.Models.db
{
    public class BranchSetup
    {
        [Key]
        public int SetupId { get; set; }
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;
        public bool EnableLocation { get; set; }
        public bool EnableNetwork { get; set; }
        public bool EnableBiometric { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
        public ICollection<BranchAllowedLocation>? AllowedLocations { get; set; }
        public ICollection<BranchAllowedNetwork>? AllowedNetworks { get; set; }
        public ICollection<BranchAllowedFace>? AllowedFaces { get; set; }
    }
}
