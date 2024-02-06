using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.SemanticKernel.Connectors.HuggingFace;

namespace HFDrive
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);
            IConfiguration config = builder.Build(); 
            try
            {
                var htToken = config["HF_TOKEN"];
                string model_id = "sentence-transformers/all-MiniLM-L6-v2"; // "intfloat/multilingual-e5-large";
                string endpoint = $"https://api-inference.huggingface.co/pipeline/feature-extraction/{model_id}";

                HttpClient httpClient = new(); // httpClientHandler);

                httpClient.BaseAddress = new Uri($"https://api-inference.huggingface.co/pipeline/feature-extraction");
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", htToken);

#pragma warning disable SKEXP0001, SKEXP0003, SKEXP0020, SKEXP0026
                HuggingFaceTextEmbeddingGenerationService embeddingService = new(model_id, httpClient);
                var emdeddings = await embeddingService.GenerateEmbeddingsAsync(["How do I get a replacement Medicare card?"]);

                IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
                kernelBuilder.Services.AddLogging(c => c.AddConsole());
                kernelBuilder.AddHuggingFaceTextEmbeddingGeneration(model_id, httpClient: httpClient);
#pragma warning restore SKEXP0001, SKEXP0003, SKEXP0011, SKEXP0026, SKEXP0050, SKEXP0052
                
                var kernel = kernelBuilder.Build();
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync(ex.Message);
            }
        }
    }
}
