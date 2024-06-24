using CLIT.OcrMicroOrchestration.Infrastructure.Handlers;
using CLIT.OcrMicroOrchestration.Infrastructure.Interfaces;
using CLIT.OcrMicroOrchestration.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();


builder.Configuration.AddJsonFile("appsettings.json");
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
builder.Services.AddScoped<IOcrService, OcrService>();
builder.Services.AddScoped<ReceiverHandler>();
builder.Services.AddScoped<IOrientationService, OrientationService>();
builder.Services.AddHostedService<ReceiverHandler>();

builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

app.UseRouting();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthorization();
app.MapControllers();
app.Run();
