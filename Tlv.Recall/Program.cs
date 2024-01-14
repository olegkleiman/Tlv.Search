using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Plugins.Core;
using StackExchange.Redis;
using System.Configuration;
using System.Net;
using Tlv.Recall;
using Tlv.Recall.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((hostContext, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddSingleton<IPromptProcessingService>(sp =>
        {
            IConfiguration configuration = sp.GetRequiredService<IConfiguration>();

            var connectionString = configuration.GetConnectionString("Redis");
            var connection = ConnectionMultiplexer.Connect(connectionString);
            Console.WriteLine("Redis connected");
            return new PromptProcessingService(connection);
        });

        services.AddSingleton<ISearchService>(sp =>
        {
            #region Read Configuration

            IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
            string? openaiAzureKey = configuration["OPENAI_AZURE_KEY"];
            string? openaiEndpoint = configuration["OPENAI_ENDPOINT"];
            string? collectionName = configuration["COLLECTION_NAME"];
            string? vectorDbHost = configuration["VECTOR_DB_HOST"];
            string? vectorDbKey = configuration["VECTOR_DB_KEY"];

            #endregion

            var searchService = new OpenAISearchService(openaiAzureKey, 
                                                          openaiEndpoint,
                                                          collectionName,
                                                          vectorDbHost,
                                                          vectorDbKey);
            Console.WriteLine("OpenAISearchService built");
            return searchService;
        });

        services.AddSingleton<Kernel>(sp =>
        {
            #region Read Configuration

            IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
            string? openaiAzureKey = configuration["OPENAI_AZURE_KEY"];
            string? openaiEndpoint = configuration["OPENAI_ENDPOINT"];
            string? proxyUrl = configuration["PROXY_URL"];

            #endregion

            HttpClientHandler? httpClientHandler = null;
            if ( !string.IsNullOrEmpty(proxyUrl) )
            {
                WebProxy webProxy = new(proxyUrl);
                // make the HttpClient instance use a proxy
                // in its requests
                httpClientHandler = new HttpClientHandler
                {
                    Proxy = webProxy
                };
            }
            HttpClient httpClient = httpClientHandler is null ? new HttpClient()
                                                                : new (httpClientHandler);

            // Initialize the SK
            IKernelBuilder kernelBuilder = Kernel.CreateBuilder()
                                        .AddAzureOpenAIChatCompletion("gpt35", //"gpt4", // Azure OpenAI Deployment Name,
                                                                 openaiEndpoint,
                                                                 openaiAzureKey,
                                                                 httpClient: httpClient);
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
