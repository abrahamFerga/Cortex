using Cortex.Cli;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("cortex");
    config.AddCommand<InitCommand>("init")
        .WithDescription("Configure a Cortex host: AI provider, knowledge pipeline, channels, storage, auth.")
        .WithExample("init", "--path", "./src/MyProduct.Host")
        .WithExample("init", "--non-interactive", "--ai-provider", "Mock", "--rag", "--embedding-provider", "Mock");
});
return app.Run(args);
