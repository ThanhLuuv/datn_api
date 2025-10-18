using BookStore.Api.Data;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Add services to the container.
builder.Services.AddControllers();

// Database configuration
builder.Services.AddDbContext<BookStoreDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    ));

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero,
        NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };
});

// Authorization with policies on 'permissions' claim (space-separated)
builder.Services.AddAuthorization(options =>
{
    bool HasPerm(System.Security.Claims.ClaimsPrincipal user, string perm)
        => user.HasClaim(c => c.Type == "permissions" && ($" {c.Value} ").Contains($" {perm} "));

    options.AddPolicy("PERM_READ_CATEGORY", p => p.RequireAssertion(ctx => HasPerm(ctx.User, "READ_CATEGORY")));
    options.AddPolicy("PERM_WRITE_CATEGORY", p => p.RequireAssertion(ctx => HasPerm(ctx.User, "WRITE_CATEGORY")));
    options.AddPolicy("PERM_READ_BOOK", p => p.RequireAssertion(ctx => HasPerm(ctx.User, "READ_BOOK")));
    options.AddPolicy("PERM_WRITE_BOOK", p => p.RequireAssertion(ctx => HasPerm(ctx.User, "WRITE_BOOK")));
    options.AddPolicy("PERM_READ_PO", p => p.RequireAssertion(ctx => HasPerm(ctx.User, "READ_PURCHASE_ORDER")));
    options.AddPolicy("PERM_WRITE_PO", p => p.RequireAssertion(ctx => HasPerm(ctx.User, "WRITE_PURCHASE_ORDER")));
    options.AddPolicy("PERM_READ_GR", p => p.RequireAssertion(ctx => HasPerm(ctx.User, "READ_GOODS_RECEIPT")));
    options.AddPolicy("PERM_WRITE_GR", p => p.RequireAssertion(ctx => HasPerm(ctx.User, "WRITE_GOODS_RECEIPT")));
});

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IPublisherService, PublisherService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<IReturnService, ReturnService>();
builder.Services.AddScoped<IPriceChangeService, PriceChangeService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddMySql(builder.Configuration.GetConnectionString("DefaultConnection")!)
    .AddDbContextCheck<BookStoreDbContext>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BookStore API", Version = "v1" });
    
    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();

// Add request/response logging middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    
    // Log request
    logger.LogInformation("=== REQUEST ===");
    logger.LogInformation("Method: {Method}", context.Request.Method);
    logger.LogInformation("Path: {Path}", context.Request.Path);
    logger.LogInformation("QueryString: {QueryString}", context.Request.QueryString);
    logger.LogInformation("Content-Type: {ContentType}", context.Request.ContentType);
    logger.LogInformation("Content-Length: {ContentLength}", context.Request.ContentLength);
    
    // Log headers
    foreach (var header in context.Request.Headers)
    {
        logger.LogInformation("Header: {Key} = {Value}", header.Key, header.Value);
    }
    
    // Log form data for multipart requests
    if (context.Request.HasFormContentType)
    {
        logger.LogInformation("=== FORM DATA ===");
        foreach (var formField in context.Request.Form)
        {
            if (formField.Key == "imageFile")
            {
                var file = context.Request.Form.Files.FirstOrDefault(f => f.Name == "imageFile");
                if (file != null)
                {
                    logger.LogInformation("File: {Key} = {FileName} ({Size} bytes)", 
                        formField.Key, file.FileName, file.Length);
                }
            }
            else
            {
                logger.LogInformation("Form: {Key} = {Value}", formField.Key, formField.Value);
            }
        }
    }
    
    // Capture response
    var originalBodyStream = context.Response.Body;
    using var responseBody = new MemoryStream();
    context.Response.Body = responseBody;
    
    await next();
    
    // Log response
    logger.LogInformation("=== RESPONSE ===");
    logger.LogInformation("Status Code: {StatusCode}", context.Response.StatusCode);
    logger.LogInformation("Content-Type: {ContentType}", context.Response.ContentType);
    
    // Log response body
    responseBody.Seek(0, SeekOrigin.Begin);
    var responseText = await new StreamReader(responseBody).ReadToEndAsync();
    logger.LogInformation("Response Body: {ResponseBody}", responseText);
    
    // Copy response back to original stream
    responseBody.Seek(0, SeekOrigin.Begin);
    await responseBody.CopyToAsync(originalBodyStream);
});

// Ensure database is created and seed data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
    context.Database.EnsureCreated();
    
    // Seed data
    await SeedData.SeedAsync(context);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowAll");

// Health Checks
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();