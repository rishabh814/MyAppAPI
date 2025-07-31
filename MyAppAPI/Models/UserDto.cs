namespace MyAppAPI.Models
{
    public class OtpRequestDto
    {
        public string Phone { get; set; }
    }

    public class VerifyOtpDto
    {
        public string Otp { get; set; }
    }

    public class ResetPasswordDto
    {
        public string NewPassword { get; set; }
    }

     public class FileUploadDto
    {
        public IFormFile File { get; set; }
    }

    // This class is used to bind incoming form-data (multipart) parameters
    public class ProfileImageUploadDto
    {
        public int UserId { get; set; }         // User ID from frontend
        public IFormFile File { get; set; }     // The uploaded image file
    }



 
   public class LeaveRequestDTO
    {
        public int EmployeeId { get; set; } // 👈 ID frontend se aayega
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; }
    }

}
