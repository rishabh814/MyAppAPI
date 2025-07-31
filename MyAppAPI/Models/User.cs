using System;
using System.Collections.Generic;

namespace MyAppAPI.Models
{
    public partial class User
    {
        public User()
        {
            LeaveRequestApprovedBies = new HashSet<LeaveRequest>();
            LeaveRequestEmployees = new HashSet<LeaveRequest>();
        }

        public int Id { get; set; }
        public string Fullname { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public string? Password { get; set; }
        public string Role { get; set; } = null!;
        public string? ProfileImage { get; set; }

        public virtual ICollection<LeaveRequest> LeaveRequestApprovedBies { get; set; }
        public virtual ICollection<LeaveRequest> LeaveRequestEmployees { get; set; }
    }
}
