using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagementAPI.Data;
using UserManagementAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Validation errors (400) use the same { "error": ... } shape.
        options.InvalidModelStateResponseFactory = ctx =>
        {
            var messages = ctx.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            return new BadRequestObjectResult(new { error = string.Join(" ", messages) });
        };
    });

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=users.db"));

var app = builder.Build();

// ORDER MATTERS:
app.UseMiddleware<ErrorHandlingMiddleware>();       // 1. Error handling (outermost)
app.UseMiddleware<TokenAuthenticationMiddleware>(); // 2. Authentication
app.UseMiddleware<RequestLoggingMiddleware>();      // 3. Logging (last)

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();