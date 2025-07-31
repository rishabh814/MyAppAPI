using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using MyAppAPI.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Add distributed cache and session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// 2. Add controllers and Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 3. Configure DB Context
var provider = builder.Services.BuildServiceProvider();
var config = provider.GetRequiredService<IConfiguration>();
builder.Services.AddDbContext<MyDBContext>(
    options => options.UseSqlServer(config.GetConnectionString("dbcs")));

// 4. ✅ Add Authentication using JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(config["Jwt:Key"])
            )
        };
    });

// 5. ✅ Add Authorization
builder.Services.AddAuthorization();

var app = builder.Build();

// 6. Create Images folder if it doesn't exist
var imageFolder = Path.Combine(Directory.GetCurrentDirectory(), "Images");
if (!Directory.Exists(imageFolder))
    Directory.CreateDirectory(imageFolder);

// 7. Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); // Show detailed errors
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ✅ Serve static files from wwwroot
app.UseStaticFiles();

// ✅ Serve /Images folder
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imageFolder),
    RequestPath = "/Images"
});

app.UseSession();

// ✅ Middleware order matters:
app.UseRouting();

app.UseAuthentication();  // 👈 Required to read token from headers
app.UseAuthorization();   // 👈 Required for [Authorize] to work

app.MapControllers();

app.Run();
