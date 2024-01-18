using Ardalis.GuardClauses;
using EmbeddingEngine.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;
using Tlv.Recall.Services;
using VectorDb.Core;

namespace Tlv.Recall
{
    public class Recall
    {
        private readonly ILogger _logger;
        private readonly IPromptProcessingService _promptService;

        public Recall(ILoggerFactory loggerFactory,
                      IPromptProcessingService promptService)
        {
            _logger = loggerFactory.CreateLogger<Recall>();
            _promptService = promptService;
        }

        protected string? GetConfigValue(string configKey)
        {
            string? value = Environment.GetEnvironmentVariable(configKey);
            Guard.Against.NullOrEmpty(value, configKey, $"Couldn't find '{configKey}' in configuration");

            return value;
        }

        [Function(nameof(Recall))]
        [OpenApiOperation(operationId: "Run", tags: new[] { "q" })]
        [OpenApiParameter(name: "q", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **prompt** parameter")]
        [OpenApiParameter(name: "l", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Query language")]
        [OpenApiParameter(name: "p", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Embedding provider name: OPENAI/GEMINI")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            try
            {
                string? prompt = req.Query["q"];
                if (string.IsNullOrEmpty(prompt))
                {
                    return new BadRequestObjectResult("Please provide some input, i.e. add ?q=... to invocation url");
                }

                _logger.LogInformation($"Executing {nameof(Recall)} with prompt {prompt}");

                //if( _promptService is not null )
                //    prompt = _promptService.FilterKeywords(prompt);

                #region Read Configuration

                string vectorDbProviderKey = GetConfigValue("VECTOR_DB_KEY")!; // ! because of previous Guard
                string vectorDbProviderHost = GetConfigValue("VECTOR_DB_HOST")!;
                string embeddingModelName = GetConfigValue("EMBEDDING_MODEL_NAME")!;

                #endregion

                string? embeddingsProviderName = req.Query["p"]; //implicit cast 
                embeddingsProviderName ??= "OPENAI";
                EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), embeddingsProviderName);

                string? lang = req.Query["l"];
                lang ??= string.Empty; // assuming Hebrew (empty)

                string configKeyName = $"{embeddingsProviderName.ToUpper()}_KEY";
                string? embeddingEngineKey = GetConfigValue(configKeyName);
                Guard.Against.NullOrEmpty(embeddingEngineKey, configKeyName, $"Couldn't find {configKeyName} in configuration");

                IEmbeddingEngine? embeddingEngine = EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                                                        providerKey: embeddingEngineKey,
                                                                                        embeddingModelName);
                Guard.Against.Null(embeddingEngine);

                ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync(prompt);

                IVectorDb? vectorDb = VectorDb.Core.VectorDb.Create(VectorDbProviders.QDrant,
                                                                    vectorDbProviderHost,
                                                                    vectorDbProviderKey);
                Guard.Against.Null(vectorDb);

                string collectionName = $"doc_parts_{embeddingEngine.ProviderName}_{embeddingEngine.ModelName}";
                if (!string.IsNullOrEmpty(lang)) // Hebrew is default (empty)
                    collectionName += $"_{lang}";
                string _collectionName = collectionName.Replace('/', '_');
                var searchResuls = await vectorDb.Search(_collectionName,
                                                         promptEmbedding);

                return new OkObjectResult(searchResuls);
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { ex.Message })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };

            }
        }
    }
}
