using System;
using System.Collections.Generic;

namespace MyAppAPI.Models
{
    public partial class LeaveRequest
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Reason { get; set; }
        public string? Status { get; set; }
        public DateTime? RequestedAt { get; set; }
        public int? ApprovedById { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public virtual User? ApprovedBy { get; set; }
        public virtual User Employee { get; set; } = null!;
    }
}
