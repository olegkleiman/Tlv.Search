using Ardalis.GuardClauses;
using EmbeddingEngine.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;
using VectorDb.Core;

namespace Tlv.Recall
{
    public class Recall
    {
        private readonly ILogger _logger;

        public Recall(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Recall>();
        }

        private string GetConfigValue(string configKey)
        {
            string? value = Environment.GetEnvironmentVariable(configKey);
            Guard.Against.NullOrEmpty(value, configKey, $"Couldn't find '{configKey}' in configuration");

            return value;
        }

        [Function(nameof(Recall))]
        [OpenApiOperation(operationId: "Run", tags: new[] { "q" })]
        [OpenApiParameter(name: "q", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **prompt** parameter")]
        [OpenApiParameter(name: "p", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Embedding provider name: OPENAI/GEMINI")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(string), Description = "The OK response")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            try
            {
                string? prompt = req.Query["q"];
                if (string.IsNullOrEmpty(prompt))
                {
                    var _response = req.CreateResponse(HttpStatusCode.BadRequest);
                    await _response.WriteStringAsync("Please provide some input, i.e. add ?q=... to invocation url");
                    return _response;
                }

                _logger.LogInformation($"Executing {nameof(Recall)} with prompt {prompt}");

                #region Read Configuration

                string collectionName = GetConfigValue("COLLECTION_NAME");
                string vectorDbProviderKey = GetConfigValue("VECTOR_DB_PROVIDER_KEY");

                #endregion

                string? embeddingsProviderName = req.Query["p"] ?? "OPENAI";
                EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), embeddingsProviderName);

                string configKeyName = $"{embeddingsProvider.ToString().ToUpper()}_KEY";
                string? embeddingEngineKey = GetConfigValue(configKeyName);
                Guard.Against.NullOrEmpty(embeddingEngineKey, configKeyName, $"Couldn't find {configKeyName} in configuration");

                IEmbeddingEngine? embeddingEngine = EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                                                        providerKey: embeddingEngineKey);
                Guard.Against.Null(embeddingEngine);

                ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync(prompt);

                IVectorDb? vectorDb = VectorDb.Core.VectorDb.Create(VectorDbProviders.QDrant, vectorDbProviderKey);
                Guard.Against.Null(vectorDb);

                var searchResuls = await vectorDb.Search($"{collectionName}_{embeddingsProviderName}", promptEmbedding);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(searchResuls);
                return response;

            }
            catch (Exception ex)
            {
                var _response = req.CreateResponse(HttpStatusCode.InternalServerError);
                _response.WriteString(ex.Message);
                return _response;
            }
        }
    }
}
