using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Connectors.Qdrant;

namespace Tlv.Recall
{
    public class Recall
    {
        private readonly ILogger _logger;

        public Recall(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Recall>();
        }

        [Function(nameof(Recall))]
        public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            IKernelBuilder kernelbuilder = Kernel.CreateBuilder();
            kernelbuilder.Services.AddLogging(c => c.AddConsole());
            var kernel = kernelbuilder.Build();

#pragma warning disable SKEXP0003, SKEXP0011, SKEXP0020, SKEXP0026, SKEXP0050, SKEXP0052, SKEXP0055
            ISemanticTextMemory memory = new MemoryBuilder()
                .WithLoggerFactory(kernel.LoggerFactory)
                .WithQdrantMemoryStore("http://localhost:6333", 1536)
                .Build();
#pragma warning restore SKEXP0003, SKEXP0011, SKEXP0020, SKEXP0026, SKEXP0050, SKEXP0052, SKEXP0055

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString("Welcome to Azure Functions!");

            return response;
        }
    }
}
