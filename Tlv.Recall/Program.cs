using Azure.AI.OpenAI;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices( (hostContext, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddHttpClient();

        services.AddSingleton(sp =>
        {
            return new ChatHistory();
        });

        services.AddSingleton<Kernel>();

        services.AddSingleton<Kernel>(sp =>
        {
            IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
            string openaiAzureKey = configuration["OPENAI_AZURE_KEY"];
            string openaiEndpoint = configuration["OPENAI_ENDPOINT"];

            // Initialize the SK
            IKernelBuilder kernelbuilder = Kernel.CreateBuilder()
                                        .AddAzureOpenAIChatCompletion("gpt35", //"gpt4", // Azure OpenAI Deployment Name,
                                                                 openaiEndpoint,
                                                                 openaiAzureKey);
            Kernel kernel = kernelbuilder.Build();

            Console.WriteLine("Kernel built");

            return kernel;

        });

    })
    .ConfigureAppConfiguration( (context, configBuilder) =>
    {
        configBuilder.AddUserSecrets<Program>();
        var config = configBuilder.Build();
        string openAiApiKey = config["OPENAI_KEY"];
    })
    .Build();

host.Run();
