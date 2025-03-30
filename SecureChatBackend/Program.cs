// backend/Program.cs
using SecureChatBackend.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSignalR();
builder.Services.AddControllers(); // If you need API controllers later

// Configure CORS (Cross-Origin Resource Sharing) to allow the Vue frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173") // Default Vite dev server port
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // Important for SignalR
        });
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // app.UseSwagger(); // Optional API docs
    // app.UseSwaggerUI(); // Optional API docs UI
}

app.UseHttpsRedirection(); // Use HTTPS if configured

app.UseCors("AllowVueApp"); // Apply the CORS policy

app.UseRouting(); // Add routing middleware

app.UseAuthorization(); // Add authorization middleware if needed later

// Map controllers if you have them
// app.MapControllers();

// Map the SignalR hub
app.MapHub<ChatHub>("/chathub"); // The endpoint clients will connect to

app.Run();
