using CASRecordingFetchJob.Helpers;
using CASRecordingFetchJob.Model;
using CASRecordingFetchJob.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Serilog;
using Serilog.Sinks.GoogleCloudLogging;
using Google.Api;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<RecordingJobDBContext>(options =>
   options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()                
    .Enrich.FromLogContext()
    .WriteTo.Console()                         
    .WriteTo.File("C:\\Logs\\log-.log",         
                  rollingInterval: RollingInterval.Day,
                  retainedFileCountLimit: 7)
    //.WriteTo.GoogleCloudLogging(projectId: "cas-prod-env")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddScoped<IRecordingJobService, RecordingJobService>();

builder.Services.AddScoped<GoogleCloudStorageHelper>();

builder.Services.AddScoped<LoggerHelper>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
