using System.ComponentModel.DataAnnotations;

namespace LetsCheckIn.Models.db
{
    /// <summary>
    /// Entity for security audit logging and compliance tracking
    /// </summary>
    public class SecurityAuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(450)] // Standard ASP.NET Identity user ID length
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string ResourceType { get; set; } = string.Empty;

        public int ResourceId { get; set; }

        [StringLength(1000)]
        public string Details { get; set; } = string.Empty;

        [StringLength(45)] // IPv6 length
        public string IPAddress { get; set; } = string.Empty;

        [StringLength(500)]
        public string UserAgent { get; set; } = string.Empty;

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Severity level: Low, Medium, High, Critical
        /// </summary>
        [StringLength(20)]
        public string Severity { get; set; } = "Medium";

        /// <summary>
        /// Whether this event represents a security violation
        /// </summary>
        public bool IsSecurityViolation { get; set; } = false;

        /// <summary>
        /// Optional reference to the session ID
        /// </summary>
        [StringLength(100)]
        public string? SessionId { get; set; }

        /// <summary>
        /// Browser or client information
        /// </summary>
        [StringLength(200)]
        public string? ClientInfo { get; set; }

        /// <summary>
        /// HTTP method used (GET, POST, etc.)
        /// </summary>
        [StringLength(10)]
        public string? HttpMethod { get; set; }

        /// <summary>
        /// Request URL path
        /// </summary>
        [StringLength(500)]
        public string? RequestPath { get; set; }

        /// <summary>
        /// Success/failure status of the operation
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Error message if operation failed
        /// </summary>
        [StringLength(1000)]
        public string? ErrorMessage { get; set; }

        // Navigation property to User
        public ApplicationUser? User { get; set; }
    }
} 