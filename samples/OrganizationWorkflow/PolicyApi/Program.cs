var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/policy", (string reason) => reason.Length >= 3);
app.Run();
