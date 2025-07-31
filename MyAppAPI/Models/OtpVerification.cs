using System;
using System.Collections.Generic;

namespace MyAppAPI.Models
{
    public partial class OtpVerification
    {
        public int Id { get; set; }
        public string Phone { get; set; } = null!;
        public string Otp { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool? IsUsed { get; set; }
    }
}
