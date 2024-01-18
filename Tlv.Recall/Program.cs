using Ardalis.GuardClauses;
using EmbeddingEngine.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Core;
using StackExchange.Redis;
using System.Configuration;
using System.Net;
using Tlv.Recall;
using Tlv.Recall.Services;
using VectorDb.Core;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices( (hostContext, services) =>
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

        services.AddSingleton<SearchService>(sp =>
        {
            #region Read Configuration

            IConfiguration configuration = sp.GetRequiredService<IConfiguration>();

            string? vectorDbHost = configuration["VECTOR_DB_HOST"];
            Guard.Against.NullOrEmpty(vectorDbHost);
            string? vectorDbKey = configuration["VECTOR_DB_KEY"];
            Guard.Against.NullOrEmpty(vectorDbKey);

            string? embeddingsProviderName = configuration["EMBEDIING_PROVIDER"];
            Guard.Against.NullOrEmpty(embeddingsProviderName);
            EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), 
                                                                                     embeddingsProviderName);
            Guard.Against.Null(embeddingsProvider);

            string configKeyName = $"{embeddingsProviderName.ToUpper()}_KEY";
            string? embeddingEngineKey = configuration[configKeyName];
            Guard.Against.NullOrEmpty(embeddingEngineKey);

            //configKeyName = $"{embeddingsProviderName.ToUpper()}_ENDPOINT";
            //string? endpoint = configuration[configKeyName];

            configKeyName = "EMBEDDING_MODEL_NAME";
            string? modelName = configuration[configKeyName];
            Guard.Against.NullOrEmpty(modelName);

            #endregion

            IVectorDb? _vectorDb = VectorDb.Core.VectorDb.Create(VectorDbProviders.QDrant,
                                                                vectorDbHost,
                                                                vectorDbKey);
            Guard.Against.Null(_vectorDb);

            IEmbeddingEngine? _embeddingEngine = EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                                providerKey: embeddingEngineKey,
                                                                modelName);
            Guard.Against.Null(_embeddingEngine);

            string _collectionName = $"doc_parts_{_embeddingEngine.ProviderName}_{_embeddingEngine.ModelName}";
            _collectionName = _collectionName.Replace('/', '_');
            return new SearchService(_vectorDb,
                                     _embeddingEngine,
                                     _collectionName);
        });

        //services.AddSingleton<IVectorDb>(sp =>
        //{
        //    #region Read Configuration

        //    IConfiguration configuration = sp.GetRequiredService<IConfiguration>();

        //    string? vectorDbHost = configuration["VECTOR_DB_HOST"];
        //    string? vectorDbKey = configuration["VECTOR_DB_KEY"];

        //    #endregion

        //    return VectorDb.Core.VectorDb.Create(VectorDbProviders.QDrant,
        //                                        vectorDbHost,
        //                                        vectorDbKey);
        //});

        //services.AddSingleton<IEmbeddingEngine>(sp =>
        //{
        //    #region Read Configuration

        //    IConfiguration configuration = sp.GetRequiredService<IConfiguration>();

        //    string? embeddingsProviderName = configuration["EMBEDIING_PROVIDER"];
        //    EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), embeddingsProviderName);

        //    string configKeyName = $"{embeddingsProviderName.ToUpper()}_KEY";
        //    string? embeddingEngineKey = configuration[configKeyName];

        //    configKeyName = $"{embeddingsProviderName.ToUpper()}_ENDPOINT";
        //    string? endpoint = configuration[configKeyName];

        //    configKeyName = "EMBEDDING_MODEL_NAME";
        //    string? modelName = configuration[configKeyName];
            
        //    #endregion

        //    return EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
        //                                                        providerKey: embeddingEngineKey,
        //                                                        modelName);
        //});

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
        //var config = configBuilder.Build();
        //string openAiApiKey = config["OPENAI_KEY"];
    })
    .Build();

host.Run();
