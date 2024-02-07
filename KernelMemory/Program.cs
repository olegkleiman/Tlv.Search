using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;

namespace KernelMemory
{
    class MyHandler(string stepName, IPipelineOrchestrator orchestrator) : IPipelineStepHandler
    {
        public string StepName => stepName;


        public Task<(bool success, DataPipeline updatedPipeline)> InvokeAsync(DataPipeline pipeline, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                //.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);
            IConfiguration config = builder.Build();

            var openaiKey = config["OPENAI_KEY"];
            var openaiEndpoint = config["OPENAI_ENDPOINT"];
            var openaiAzureKey = config["OPENAI_AZURE_KEY"];

            var chatConfig = new AzureOpenAIConfig
            {
                APIKey = openaiAzureKey,
                Deployment = "gpt4", // Azure OpenAI Deployment Name
                Endpoint = openaiEndpoint,
                APIType = AzureOpenAIConfig.APITypes.ChatCompletion,
                Auth = AzureOpenAIConfig.AuthTypes.APIKey
            };

            // Memory setup, e.g. how to calculate and where to store embeddings
            var memoryBuilder = new KernelMemoryBuilder()
                                //.WithAzureOpenAITextGeneration(chatConfig)
                                .WithOpenAIDefaults(openaiKey);

            var memory = memoryBuilder.Build<MemoryServerless>();

            var plugin = new MemoryPlugin(memory, waitForIngestionToComplete: true);
            
            //IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
            //kernelBuilder.AddAzureOpenAITextGeneration("gpt35tubro16K",
            //                                            "https://curiosity.openai.azure.com/",
            //                                            "1caa8932bf794b3b9046446b65130822");
            //var kernel = kernelBuilder.Build();
            //kernel.ImportPluginFromObject(plugin, "memory");

            //// Part 1. Function calling
            //OpenAIPromptExecutionSettings settings = new()
            //{
            //    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            //};
            //var prompt = @"
            //Question to Kernel Memory: {{$input}}

            //Kernel Memory Answer: {{memory.ask}}

            //If the answer is empty say 'I don't know', otherwise reply with a business mail to share the answer.
            //";
            //KernelArguments arguments = new KernelArguments(settings)
            //{
            //    { "input", "What is Contoso Electronics?" },
            //};
            //var response = await kernel.InvokePromptAsync(prompt, arguments);
            //Console.WriteLine(response.GetValue<string>());

            var orchestrator = memoryBuilder.GetOrchestrator();
            await orchestrator.AddHandlerAsync(new MyHandler("step1", orchestrator));
            var pipeline = 
                    orchestrator.PrepareNewDocumentUpload("user-id-1", "docId", tags: new() { { "user", "Oleg" } })
                    .Then("step1")
                    .Build();
            // Execute in process, process all files with all the handlers
            //await orchestrator.RunPipelineAsync(pipeline);
            
            await memory.ImportTextAsync("User name: oleg_kleyman", documentId: "auden01");
            await memory.ImportTextAsync("Interested only in sport, art, science", documentId: "auden02");
            await memory.ImportDocumentAsync("NASA-news.pdf", tags: new() { { "user", "Blake" } });
            await memory.ImportDocumentAsync("sample-SK-Readme.pdf");

            var answer = await memory.AskAsync("Are the user interested in space?");
            Console.WriteLine(answer.Result);

            foreach (var x in answer.RelevantSources)
            {
                Console.WriteLine($"  * {x.SourceName} -- {x.Partitions.First().LastUpdate:D}");
            }

            await memory.DeleteDocumentAsync("auden01");
        }
    }
}
