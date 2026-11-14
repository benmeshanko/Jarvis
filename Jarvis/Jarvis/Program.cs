using Jarvis.Database;
using Jarvis.Services;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<LibraryService>();
builder.Services.AddSingleton<MediaService>();
builder.Services.AddSingleton<StatisticsService>();
builder.Services.AddHostedService(p => p.GetRequiredService<StatisticsService>());
builder.Services.AddHostedService<Worker>();

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite("Data Source=./jarvis.db"));

var host = builder.Build();

using var scope = host.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
dbContext.Database.Migrate();

host.Run();
