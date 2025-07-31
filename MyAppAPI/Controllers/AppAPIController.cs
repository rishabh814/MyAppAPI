using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.IdentityModel.Tokens;
using MyAppAPI.Models;
using Newtonsoft.Json;


////////
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Xml.Linq;


namespace MyAppAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppAPIController : ControllerBase
    {
        private readonly MyDBContext context;
        private readonly IConfiguration config; ///////////////

        public AppAPIController(MyDBContext context, IConfiguration config)   //////////////
        {
            this.context = context;
            this.config = config;   ///////////

        }

        //for registration
        //User is the model class (like a table schema) — it represents a row in the Users table.
        //USER(capitalized) is just the name of the variable. isime table ke user ka saara detail hoga 
        [HttpPost("Register")] // Route: POST /Register
        public async Task<ActionResult<User>> RegisterUser(User USER)
        {
            // ✅ 1. Check if the incoming request body is valid
            if (USER == null || string.IsNullOrEmpty(USER.Email) || string.IsNullOrEmpty(USER.Password))
            {
                return BadRequest(new
                {
                    status = "error",
                    message = "Email and Password are required." // Error if required fields are missing
                });
            }

            try
            {
                // ✅ 2. Check if the user already exists in the database by email
                var existingUser = await context.Users
                    .FirstOrDefaultAsync(u => u.Email == USER.Email);

                if (existingUser != null)
                {
                    return BadRequest(new
                    {
                        status = "error",
                        message = "User already exists with this email." // Avoid duplicate registrations
                    });
                }

                // ✅ 3. Hash the user's password using BCrypt before saving to the DB
                USER.Password = BCrypt.Net.BCrypt.HashPassword(USER.Password);

                // ✅ 4. Save the new user to the database
                await context.Users.AddAsync(USER); // Adds the new user entity
                await context.SaveChangesAsync();   // Commits the changes to the database

                // ✅ 5. Prepare the response without sensitive data like password
                var responseUser = new
                {
                    USER.Id,        // Assuming the DB generates this automatically
                    USER.Email,
                    USER.Fullname,
                    USER.Phone      // You can customize what fields to expose in response
                };

                // ✅ 6. Return a success message and user details
                return Ok(new
                {
                    status = "success",
                    message = "User registered successfully",
                    user = responseUser
                });
            }
            catch (Exception ex)
            {
                // ✅ 7. Handle unexpected errors gracefully
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An unexpected error occurred.",
                    error = ex.Message // In production, avoid sending detailed errors
                });
            }
        }



        //to login
        [HttpPost("login")]
        public async Task<IActionResult> LoginUser([FromForm] string email, [FromForm] string password)
        {
            // ✅ Validate input: check if email or password is empty/null/whitespace
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return BadRequest(new
                {
                    status = "error",
                    message = "Email and Password are required." // ❌ Missing input
                });
            }

            try
            {
                // 🔍 Search for user by email in the database
                var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);

                // ❌ If user is not found or password is incorrect, return 401 Unauthorized
                if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.Password))
                {
                    return Unauthorized(new
                    {
                        status = "error",
                        message = "Invalid email or password." // ❌ Authentication failed
                    });
                }

                // 🔐 Setup JWT token handler and secret key
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(config["Jwt:Key"]); // 🔑 Get secret key from config (e.g., appsettings.json)

                // ✅ Define JWT claims (here: only user ID is included)
                var claims = new[]
                {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) // 📌 Claim: User ID
        };

                // 📦 Create token descriptor with claims, expiration, and signing credentials
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),                  // 👤 Claims identity
                    Expires = DateTime.UtcNow.AddMinutes(60),              // ⏰ Token expiration time (1 hour)
                    SigningCredentials = new SigningCredentials(           // 🔏 Signing token with symmetric key
                        new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature
                    )
                };

                // 🛠 Generate the token
                var token = tokenHandler.CreateToken(tokenDescriptor);

                // 📝 Write the token as a string
                var jwtToken = tokenHandler.WriteToken(token);

                // ✅ Return 200 OK with token and basic user info
                return Ok(new
                {
                    status = "success",
                    message = "Login successful",
                    token = jwtToken, // 📤 JWT to be used in future authenticated requests
                    user = new
                    {
                        user.Id,
                        user.Email,
                        user.Fullname // 👤 Return basic user info
                    }
                });
            }
            catch (Exception ex)
            {
                // ❗ Catch unexpected errors and return 500 Internal Server Error
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An unexpected error occurred.",
                    error = ex.Message // ⚠️ Error details (consider hiding in production)
                });
            }
        }





        //send otp api 
        [HttpPost("send-otp")] // Defines a POST API endpoint at /send-otp
        public async Task<IActionResult> SendOtp([FromBody] OtpRequestDto request)
        {
            // ✅ 1. Validate input: phone number must be provided
            if (string.IsNullOrEmpty(request.Phone))
                return BadRequest(new { status = "error", message = "Phone number is required." });

            // ✅ 2. Check if a registered user exists with the given phone number
            var user = await context.Users.FirstOrDefaultAsync(u => u.Phone == request.Phone);
            if (user == null)
                return BadRequest(new { status = "error", message = "User not registered." });

            // ✅ 3. Generate a random 6-digit OTP
            var otp = new Random().Next(100000, 999999).ToString();

            // ✅ 4. Prepare the SMS message content
            var message = $"Your OTP is {otp}";

            // ✅ 5. Build the SMS API URL (2Factor Custom SMS API)
            var smsUrl = $"https://2factor.in/API/V1/7bf51f1a-6630-11f0-a562-0200cd936042/SMS/{request.Phone}/{otp}/OTP1";

            // ✅ 6. Send the SMS using HTTP GET request
            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(smsUrl);

            // ❌ 7. If the SMS sending fails, return error
            if (!response.IsSuccessStatusCode)
                return StatusCode(500, new { status = "error", message = "OTP sending failed." });

            // ✅ 8. Create a new OTP record for verification
            var otpRecord = new OtpVerification
            {
                Phone = request.Phone,                    // Phone number for which OTP is sent
                Otp = otp,                                // The generated OTP
                CreatedAt = DateTime.Now,                 // Timestamp when OTP was created
                ExpiresAt = DateTime.Now.AddMinutes(5),   // OTP is valid for 5 minutes
                IsUsed = false                            // Initially, OTP is not used
            };

            // ✅ 9. Save the OTP record to the database
            context.OtpVerifications.Add(otpRecord);
            await context.SaveChangesAsync();

            // ✅ 10. Return success response
            return Ok(new { status = "success", message = "OTP sent successfully." });
        }






        //verify otp (no use of api verified from database) 
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto request)
        {
            if (string.IsNullOrEmpty(request.Otp))
                return BadRequest(new { status = "error", message = "OTP is required." });

            var otpRecord = await context.OtpVerifications
                .Where(o => o.Otp == request.Otp && (o.IsUsed == false || o.IsUsed == null) && o.ExpiresAt > DateTime.Now)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otpRecord == null)
                return BadRequest(new { status = "error", message = "Invalid or expired OTP." });

            otpRecord.IsUsed = true;
            await context.SaveChangesAsync();

            HttpContext.Session.SetString("verifiedPhone", otpRecord.Phone);

            return Ok(new { status = "success", message = "OTP verified successfully." });
        }





        //reset password api

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
        {
            if (string.IsNullOrEmpty(request.NewPassword))
                return BadRequest(new { status = "error", message = "New password is required." });

            var phone = HttpContext.Session.GetString("verifiedPhone");
            if (string.IsNullOrEmpty(phone))
                return BadRequest(new { status = "error", message = "OTP verification required first." });

            var user = await context.Users.FirstOrDefaultAsync(u => u.Phone == phone);
            if (user == null)
                return BadRequest(new { status = "error", message = "User not found." });

            user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await context.SaveChangesAsync();

            HttpContext.Session.Remove("verifiedPhone");

            return Ok(new { status = "success", message = "Password reset successfully." });
        }


        //To upload image

        [HttpPost("upload-image")] // Defines POST endpoint at /upload-image
        [Consumes("multipart/form-data")] // ✅ Required for file uploads in Swagger or Postman
        public async Task<IActionResult> UploadImage([FromForm] FileUploadDto data)
        {
            try
            {
                // ✅ 1. Validate if file is present
                if (data.File == null || data.File.Length == 0)
                {
                    return BadRequest(new
                    {
                        status = "error",
                        message = "No file was uploaded." // Error if file is missing or empty
                    });
                }

                // ✅ 2. Define upload path: /Images folder in root directory
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Images");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder); // Create folder if it doesn't exist

                // ✅ 3. Create unique file name to avoid collisions
                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(data.File.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // ✅ 4. Save uploaded file to disk
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await data.File.CopyToAsync(stream); // Asynchronously save file to server
                }

                // ✅ 5. Save file info to database (only file name/path)
                var image = new Image { ImagePath = uniqueFileName };
                context.Images.Add(image);
                await context.SaveChangesAsync();

                // ✅ 6. Return success response with image URL
                return Ok(new
                {
                    status = "success",
                    message = "Image uploaded successfully",
                    imageUrl = $"{Request.Scheme}://{Request.Host}/Images/{uniqueFileName}"
                });
            }
            catch (Exception ex)
            {
                // ✅ 7. Catch any unexpected error
                return StatusCode(500, new
                {
                    status = "error",
                    message = "Server error while uploading image.",
                    error = ex.Message
                });
            }
        }





        [HttpGet("all-images")] // Defines GET endpoint at /all-images
        public IActionResult GetAllImages()
        {
            try
            {
                // ✅ 1. Fetch all images from DB and project with full URLs
                var images = context.Images
                    .Select(img => new
                    {
                        img.Id,
                        url = $"{Request.Scheme}://{Request.Host}/Images/{img.ImagePath}"
                    })
                    .ToList();

                // ✅ 2. Return success response with image list
                return Ok(new
                {
                    status = "success",
                    count = images.Count,
                    data = images
                });
            }
            catch (Exception ex)
            {
                // ✅ 3. Return server error if DB query fails
                return StatusCode(500, new
                {
                    status = "error",
                    message = "Server error while fetching images.",
                    error = ex.Message
                });
            }
        }




        [HttpDelete("delete-image/{id}")] // Defines DELETE endpoint at /delete-image/{id}
        public async Task<IActionResult> DeleteImage(int id)
        {
            try
            {
                // ✅ 1. Find image by ID from DB
                var image = await context.Images.FindAsync(id);
                if (image == null)
                {
                    return NotFound(new
                    {
                        status = "error",
                        message = $"Image with ID {id} not found." // Return 404 if not found
                    });
                }

                // ✅ 2. Build full path to physical image file
                var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "Images", image.ImagePath);

                // ✅ 3. If file exists on disk, delete it
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath); // Delete physical file
                }

                // ✅ 4. Remove image record from DB
                context.Images.Remove(image);
                await context.SaveChangesAsync();

                // ✅ 5. Return success message
                return Ok(new
                {
                    status = "success",
                    message = $"Image with ID {id} deleted successfully."
                });
            }
            catch (Exception ex)
            {
                // ✅ 6. Catch unexpected errors
                return StatusCode(500, new
                {
                    status = "error",
                    message = "Server error while deleting image.",
                    error = ex.Message
                });
            }
        }







        // ✅ GET: api/AppAPI/details
        [HttpGet("details")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var data = await context.InfoTables.ToListAsync();
                if (data == null || data.Count == 0)
                    return NotFound(new { status = "error", message = "No data found." });

                return Ok(new { status = "success", data = data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "error", message = $"Server error: {ex.Message}" });
            }
        }


        // ✅ POST: api/AppAPI/additem
        [HttpPost("additem")]
        public async Task<IActionResult> AddItem([FromBody] InfoTable item)
        {
            try
            {
                if (item == null)
                    return BadRequest(new { status = "error", message = "Request body is missing." });

                if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Detail))
                    return BadRequest(new { status = "error", message = "Name and Detail fields are required." });

                context.InfoTables.Add(item);
                await context.SaveChangesAsync();

                return Ok(new { status = "success", message = "Item added successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "error", message = $"Server error: {ex.Message}" });
            }
        }

        // ✅ PUT: api/AppAPI/update/{id}
        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateItem(int id, [FromBody] InfoTable item)
        {
            if (id != item.Id)
                return BadRequest(new { status = "error", message = "ID mismatch." });

            try
            {
                var existing = await context.InfoTables.FindAsync(id);
                if (existing == null)
                    return NotFound(new { status = "error", message = "Item not found." });

                existing.Name = item.Name;
                existing.Detail = item.Detail;

                context.Entry(existing).State = EntityState.Modified;
                await context.SaveChangesAsync();

                return Ok(new { status = "success", message = "Item updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "error", message = $"Server error: {ex.Message}" });
            }
        }


        // ✅ DELETE: api/AppAPI/delete/{id}
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            try
            {
                var item = await context.InfoTables.FindAsync(id);
                if (item == null)
                    return NotFound(new { status = "error", message = "Item not found." });

                context.InfoTables.Remove(item);
                await context.SaveChangesAsync();

                return Ok(new { status = "success", message = "Item deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "error", message = $"Server error: {ex.Message}" });
            }
        }











        //fetches user detail their image, name,role
        [HttpGet("user-infowithprofileimagenameandrole")]
        public IActionResult GetAllUserInfo()
        {
            try
            {
                var users = context.Users
                    .Select(user => new
                    {
                        name = user.Fullname,
                        role = user.Role,
                        imageUrl = string.IsNullOrEmpty(user.ProfileImage)
                            ? null
                            : $"{Request.Scheme}://{Request.Host}/Images/{user.ProfileImage}"
                    })
                    .ToList();

                if (users == null || users.Count == 0)
                {
                    return NotFound(new
                    {
                        status = "error",
                        message = "No users found."
                    });
                }

                return Ok(new
                {
                    status = "success",
                    data = users
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "error",
                    message = ex.Message
                });
            }
        }



        //delete profile image api
        [HttpDelete("delete-profile-image/{id}")]
        public async Task<IActionResult> DeleteProfileImage(int id)
        {
            try
            {
                // ✅ Fetch user by ID
                var user = await context.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { status = "error", message = "User not found." });

                // ✅ Check if image exists
                if (string.IsNullOrEmpty(user.ProfileImage))
                    return BadRequest(new { status = "error", message = "No image to delete." });

                // ✅ Delete image from wwwroot/Images
                var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Images", user.ProfileImage);
                if (System.IO.File.Exists(imagePath))
                    System.IO.File.Delete(imagePath);

                // ✅ Clear image reference in DB
                user.ProfileImage = null;
                await context.SaveChangesAsync();

                // ✅ Success message
                return Ok(new { status = "success", message = "Profile image deleted." });
            }
            catch (Exception ex)
            {
                // ❌ Error
                return StatusCode(500, new { status = "error", message = ex.Message });
            }
        }



        //to get only image by id
        [HttpGet("get-profile-image/{id}")]
        public async Task<IActionResult> GetProfileImageById(int id)
        {
            try
            {
                // 🔍 Step 1: Look for the user with the given ID
                var user = await context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new
                    {
                        status = "error",
                        message = "User not found"
                    });
                }

                // ❗ Step 2: Check if user has an image
                if (string.IsNullOrEmpty(user.ProfileImage))
                {
                    return NotFound(new
                    {
                        status = "error",
                        message = "User has no profile image"
                    });
                }

                // ✅ Step 3: Construct the public image URL
                var imageUrl = $"{Request.Scheme}://{Request.Host}/Images/{user.ProfileImage}";

                return Ok(new
                {
                    status = "success",
                    imageUrl = imageUrl
                });
            }
            catch (Exception ex)
            {
                // 🚨 Catch any unexpected error
                return StatusCode(500, new
                {
                    status = "error",
                    message = ex.Message
                });
            }
        }




        [HttpPost("upload-profile-image")]
        [Consumes("multipart/form-data")] // This allows Swagger and others to handle file uploads correctly
        public async Task<IActionResult> UploadProfileImage([FromForm] ProfileImageUploadDto data)
        {
            try
            {
                // ✅ Step 1: Find user by ID
                var user = await context.Users.FindAsync(data.UserId);
                if (user == null)
                {
                    // ❌ If user doesn't exist, return error
                    return NotFound(new { status = "error", message = "User not found." });
                }

                // ✅ Step 2: Check if file is sent and not empty
                if (data.File == null || data.File.Length == 0)
                {
                    return BadRequest(new { status = "error", message = "No image uploaded." });
                }

                // ✅ Step 3: Create Images folder if it doesn't exist
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Images");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                // ✅ Step 4: Generate unique filename to avoid duplicate names
                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(data.File.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // ✅ Step 5: Save the uploaded image to server's wwwroot/Images folder
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await data.File.CopyToAsync(stream);
                }

                // ✅ Step 6: If user already had an image, delete the old one
                if (!string.IsNullOrEmpty(user.ProfileImage))
                {
                    var oldFilePath = Path.Combine(uploadsFolder, user.ProfileImage);
                    if (System.IO.File.Exists(oldFilePath))
                        System.IO.File.Delete(oldFilePath);
                }

                // ✅ Step 7: Save new image name in database
                user.ProfileImage = uniqueFileName;
                await context.SaveChangesAsync();

                // ✅ Step 8: Return success with full image URL
                return Ok(new
                {
                    status = "success",
                    imageUrl = $"{Request.Scheme}://{Request.Host}/Images/{uniqueFileName}"
                });
            }
            catch (Exception ex)
            {
                // ❌ Step 9: If any error happens during process, return 500 error
                return StatusCode(500, new { status = "error", message = ex.Message });
            }
        }







        // ✅ 1. Create a new leave request
        [HttpPost("requestLeaveApi")]
        public async Task<IActionResult> CreateLeaveRequest([FromBody] LeaveRequestDTO model)
        {
            try
            {
                // ✅ Validate the incoming model
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        status = "error",
                        message = "Validation failed",
                        errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                    });
                }

                // ✅ Ensure EndDate is not earlier than StartDate
                if (model.EndDate < model.StartDate)
                {
                    return BadRequest(new
                    {
                        status = "error",
                        message = "End date cannot be earlier than start date."
                    });
                }

                // ✅ Create and save the leave request
                var request = new LeaveRequest
                {
                    EmployeeId = model.EmployeeId,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    Reason = model.Reason,
                    Status = "Pending",             // Leave is pending by default
                    RequestedAt = DateTime.UtcNow
                };

                context.LeaveRequests.Add(request);
                await context.SaveChangesAsync();

                return Ok(new
                {
                    status = "success",
                    message = "Leave request submitted successfully",
                    data = request
                });
            }
            catch (Exception ex)
            {
                // ✅ Catch unexpected errors
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An unexpected error occurred.",
                    error = ex.Message
                });
            }
        }

        // ✅ 2. Approve a leave request using LeaveRequest.Id leave req tables id
        [HttpPost("approve-leave-admin/{id}")]
        public async Task<IActionResult> ApproveLeaveRequest(int id)
        {
            try
            {
                // ✅ Find the request by ID and ensure it's still pending
                var request = await context.LeaveRequests.FindAsync(id);
                if (request == null || request.Status != "Pending")
                {
                    return NotFound(new
                    {
                        status = "error",
                        message = "Pending leave request not found"
                    });
                }

                // ✅ Update the status and approval time
                request.Status = "Approved";
                request.ApprovedAt = DateTime.UtcNow;

                await context.SaveChangesAsync();

                return Ok(new
                {
                    status = "success",
                    message = "Leave request approved",
                    data = request
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An unexpected error occurred.",
                    error = ex.Message
                });
            }
        }

        // ✅ 3. Reject a leave request using LeaveRequest.Id
        [HttpPost("rejectleave/{id}")]
        public async Task<IActionResult> RejectLeaveRequest(int id)
        {
            try
            {
                // ✅ Find the request by ID and ensure it's still pending
                var request = await context.LeaveRequests.FindAsync(id);
                if (request == null || request.Status != "Pending")
                {
                    return NotFound(new
                    {
                        status = "error",
                        message = "Pending leave request not found"
                    });
                }

                // ✅ Update status to Rejected and mark approval timestamp
                request.Status = "Rejected";
                request.ApprovedAt = DateTime.UtcNow;

                await context.SaveChangesAsync();

                return Ok(new
                {
                    status = "success",
                    message = "Leave request rejected",
                    data = request
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An unexpected error occurred.",
                    error = ex.Message
                });
            }
        }

        // ✅ 4. Get all leave requests for a specific employee
        [HttpGet("my-leaves-user")]
        public async Task<IActionResult> GetMyLeaves([FromQuery] int employeeId)
        {
            try
            {
                // ✅ Fetch all leave requests for given employee, most recent first
                var leaves = await context.LeaveRequests
                    .Where(lr => lr.EmployeeId == employeeId)
                    .OrderByDescending(lr => lr.RequestedAt)
                    .ToListAsync();

                return Ok(new
                {
                    status = "success",
                    message = "Leave requests fetched successfully",
                    data = leaves
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An unexpected error occurred.",
                    error = ex.Message
                });
            }
        }

        // ✅ 5. Get all pending leave requests (for Admin)
        [HttpGet("allLeavesforadmin")]
        public async Task<IActionResult> GetAllPendingLeaveRequests()
        {
            try
            {
                // ✅ Fetch all leave requests that are still pending
                var pendingRequests = await context.LeaveRequests
                    .Where(lr => lr.Status == "Pending")
                    .OrderByDescending(lr => lr.RequestedAt)
                    .Select(lr => new
                    {
                        lr.Id,
                        lr.EmployeeId,
                        lr.StartDate,
                        lr.EndDate,
                        lr.Reason,
                        lr.Status,
                        lr.RequestedAt
                    })
                    .ToListAsync();

                // ✅ If none are found, send an empty list with a clear message
                if (pendingRequests.Count == 0)
                {
                    return Ok(new
                    {
                        status = "success",
                        message = "No pending leave requests found.",
                        data = new List<object>()
                    });
                }

                return Ok(new
                {
                    status = "success",
                    message = "Pending leave requests fetched successfully",
                    data = pendingRequests
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An unexpected error occurred.",
                    error = ex.Message
                });
            }
        }
    }
}
