    using System.Net;
using Ardalis.GuardClauses;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Tlv.Recall
{
    public class Model
    {
        private readonly ILogger _logger;

        public Model(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Model>();
        }

        protected string? GetConfigValue(string configKey)
        {
            string? value = Environment.GetEnvironmentVariable(configKey);
            Guard.Against.NullOrEmpty(value, configKey, $"Couldn't find '{configKey}' in configuration");

            return value;
        }

        [Function("Model")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "model")] HttpRequestData req)
        {
            string embeddingModelName = GetConfigValue("EMBEDDING_MODEL_NAME")!;

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            try
            {
                await response.WriteStringAsync(embeddingModelName);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return response;
        }
    }
}
