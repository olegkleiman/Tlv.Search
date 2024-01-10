using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Plugins.Core;
using Tlv.Recall.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((hostContext, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddSingleton<ISearchService>(sp =>
        {
            #region Read Configuration

            IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
            string openaiAzureKey = configuration["OPENAI_AZURE_KEY"];
            string openaiEndpoint = configuration["OPENAI_ENDPOINT"];
            string collectionName = configuration["COLLECTION_NAME"];
            string vectorDbHostUrl = configuration["VECTOR_DB_PROVIDER_HOST"];
            string vectorDbProviderKey = configuration["VECTOR_DB_PROVIDER_KEY"];

            #endregion

            var searchService = new OpenAISearchService(openaiAzureKey, 
                                                          openaiEndpoint,
                                                          collectionName,
                                                          vectorDbHostUrl,
                                                          vectorDbProviderKey);
            Console.WriteLine("OpenAISearchService built");
            return searchService;
        });

        services.AddSingleton<Kernel>(sp =>
        {
            #region Read Configuration

            IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
            string openaiAzureKey = configuration["OPENAI_AZURE_KEY"];
            string openaiEndpoint = configuration["OPENAI_ENDPOINT"];

            #endregion

            // Initialize the SK
            IKernelBuilder kernelBuilder = Kernel.CreateBuilder()
                                        .AddAzureOpenAIChatCompletion("gpt35", //"gpt4", // Azure OpenAI Deployment Name,
                                                                 openaiEndpoint,
                                                                 openaiAzureKey);
            kernelBuilder.Services.AddLogging( logginBuilder =>
            {
                logginBuilder.AddFilter(level => true);
                logginBuilder.AddConsole();
            });
                
#pragma warning disable SKEXP0050
            kernelBuilder.Plugins.AddFromType<HttpPlugin>();
#pragma warning restore  SKEXP0050
            Kernel kernel = kernelBuilder.Build();

            Console.WriteLine("Kernel built");

            return kernel;

        });

    })
    .ConfigureAppConfiguration((context, configBuilder) =>
    {
        configBuilder.AddUserSecrets<Program>();
        var config = configBuilder.Build();
        string openAiApiKey = config["OPENAI_KEY"];
    })
    .Build();

host.Run();
