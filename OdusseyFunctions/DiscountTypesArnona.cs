using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace OdusseyFunctions
{
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
