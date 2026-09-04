// Composition only (ARQ-01): what the worker is made of lives in HostingExtensions; nothing else belongs here.
using Coliseum.Worker;

var builder = WebApplication.CreateBuilder(args).AddColiseumWorker();
var app = builder.Build().UseColiseumWorker();
app.Run();
