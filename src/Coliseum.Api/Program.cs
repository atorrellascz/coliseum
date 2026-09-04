// Composition only (ARQ-01): what the API is made of lives in HostingExtensions; nothing else belongs here.
using Coliseum.Api;

var builder = WebApplication.CreateBuilder(args).AddColiseumApi();
var app = builder.Build().UseColiseumApi();
app.Run();
