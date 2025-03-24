using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Register KeyManager as a singleton with SQLite connection string
builder.Services.AddSingleton<KeyManager>(new KeyManager("Data Source=totally_not_my_privateKeys.db"));

// Add controllers
builder.Services.AddControllers();

var app = builder.Build();

// Initialize the KeyManager with sample keys
var keyManager = app.Services.GetRequiredService<KeyManager>();
keyManager.GenerateKey(DateTime.UtcNow.AddHours(1)); // Valid key
keyManager.GenerateKey(DateTime.UtcNow.AddHours(-1)); // Expired key

// Configure middleware
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

Console.WriteLine("Current working directory: " + Directory.GetCurrentDirectory());
app.Run();