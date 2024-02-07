using Ardalis.GuardClauses;
using EmbeddingEngine.Core;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Core;
using StackExchange.Redis;
using System.Net;
using Tlv.Search.Services;
using VectorDb.Core;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((context, config) =>
    {
        // Load configuration from appsettings.json
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        
    })
    .ConfigureServices((hostContext, services) =>
    {
        string? appInsightsConnectionString = Environment.GetEnvironmentVariable("AzureApplicationInsightsConnectionString");
        Guard.Against.NullOrEmpty(appInsightsConnectionString);
        var options = new ApplicationInsightsServiceOptions
        {
            ConnectionString = appInsightsConnectionString
        };
        services.AddApplicationInsightsTelemetry(options: options);

        services.AddSingleton<IPromptProcessingService>(sp =>
        {
            IConfiguration configuration = sp.GetRequiredService<IConfiguration>();

            string? redisConnectionString = configuration.GetConnectionString("Redis");
            if(redisConnectionString.IsNullOrEmpty() )
                return new NullPropmtProcessng();
            else
            {
                var connection = ConnectionMultiplexer.Connect(redisConnectionString);
                Console.WriteLine("Redis connected");
                return new FrequencyFilterPromptProcessing(connection);
            }
        });

        services.AddSingleton<SearchService>(sp =>
        {
            #region Read Configuration

            string? vectorDbHost = Environment.GetEnvironmentVariable("VECTOR_DB_HOST");
            Guard.Against.NullOrEmpty(vectorDbHost);
            string? vectorDbKey = Environment.GetEnvironmentVariable("VECTOR_DB_KEY");
            Guard.Against.NullOrEmpty(vectorDbKey);

            string? embeddingsProviderName = Environment.GetEnvironmentVariable("EMBEDIING_PROVIDER");
            Guard.Against.NullOrEmpty(embeddingsProviderName);
            EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders),
                                                                                     embeddingsProviderName);
            Guard.Against.Null(embeddingsProvider);

            string configKeyName = $"{embeddingsProviderName.ToUpper()}_KEY";
            string? embeddingEngineKey = Environment.GetEnvironmentVariable(configKeyName);
            Guard.Against.NullOrEmpty(embeddingEngineKey);

            configKeyName = $"{embeddingsProviderName.ToUpper()}_ENDPOINT";
            string? endpoint = Environment.GetEnvironmentVariable(configKeyName);
            Guard.Against.NullOrEmpty(endpoint);

            configKeyName = "EMBEDDING_MODEL_NAME";
            string? modelName = Environment.GetEnvironmentVariable(configKeyName); 
            Guard.Against.NullOrEmpty(modelName);

            configKeyName = "COLLECTION_NAME_PREFIX";
            string? collectionPrefix = Environment.GetEnvironmentVariable(configKeyName);
            //Guard.Against.NullOrEmpty(collectionPrefix); - collection prefix name could be empty

            #endregion

            IVectorDb? _vectorDb = VectorDb.Core.VectorDb.Create(VectorDbProviders.QDrant,
                                                                vectorDbHost,
                                                                vectorDbKey);
            Guard.Against.Null(_vectorDb);

            IEmbeddingEngine? embeddingEngine = EmbeddingEngine.Core.EmbeddingEngine.Create(
                                                                embeddingsProvider,
                                                                providerKey: embeddingEngineKey,
                                                                endpoint: endpoint,
                                                                modelName);
            Guard.Against.Null(embeddingEngine);

            configKeyName = "COLLECTION_NAME_PREFIX";
            string? collectionNamePrefix = Environment.GetEnvironmentVariable(configKeyName);
            Guard.Against.NullOrEmpty(collectionNamePrefix);

            string _collectionName = $"{collectionNamePrefix}_{embeddingEngine.ProviderName}_{embeddingEngine.ModelName}";
            _collectionName = _collectionName.Replace('/', '_');
            return new SearchService(_vectorDb,
                                     embeddingEngine,
                                     _collectionName);
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
            if (!string.IsNullOrEmpty(proxyUrl))
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
                                                                : new(httpClientHandler);

            // Initialize the SK
            IKernelBuilder kernelBuilder = Kernel.CreateBuilder()
                                        .AddAzureOpenAIChatCompletion("gpt35", //"gpt4", // Azure OpenAI Deployment Name,
                                                                 openaiEndpoint,
                                                                 openaiAzureKey,
                                                                 httpClient: httpClient);
            kernelBuilder.Services.AddLogging(logginBuilder =>
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
        //var config = configBuilder.Build();
        //string openAiApiKey = config["OPENAI_KEY"];
    })
    .Build();

host.Run();
