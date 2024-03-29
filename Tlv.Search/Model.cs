using System.Net;
using Ardalis.GuardClauses;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Tlv.Search
{
    public class Model
    {
        private readonly TelemetryClient _telemetryClient;

        public Model()
        {
            _telemetryClient = new TelemetryClient();
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
            _telemetryClient?.TrackTrace($"embeddingModelName:{embeddingModelName}");
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            try
            {
                await response.WriteStringAsync(embeddingModelName);
            }
            catch(Exception ex)
            {
                _telemetryClient?.TrackException(ex);
                _telemetryClient?.TrackTrace($"Contact to perform the search from {nameof(Model)} with error:{ex.Message}");

            }

            return response;
        }
    }
}
