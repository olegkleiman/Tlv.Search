using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace OdysseyFunctions
{
    public record Field
    {
        public string Caption { get; set; }
        public string InternalName { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
    };

    public record DiscountTypesArnonaPayload
    {
        public string Attachments { get; set; }
        public Field[] Fields { get; set; }
    }

    public class DiscountTypesArnona
    {
        private readonly ILogger _logger;

        public DiscountTypesArnona(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<DiscountTypesArnona>();
        }

        [Function(nameof(DiscountTypesArnona))]
        public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            // Read passed HTML content as JSON
            //req.Body.ReadAsync();
            string? body = req.ReadAsString();
            var payload = JsonSerializer.Deserialize<DiscountTypesArnonaPayload[]>(body);

            // TBD
            // Exctract the info we interested and save it inot VectorDb (Qdrant)
            // 

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString("Welcome to Azure Functions!");

            return response;
        }
    }
}
