using Jarvis.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<LibraryService>();
builder.Services.AddSingleton<MediaService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
