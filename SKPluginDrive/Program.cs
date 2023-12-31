using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.TextGeneration;
using System.Net.Sockets;
using System.Threading;
using static System.Formats.Asn1.AsnWriter;

namespace SKPluginDrive
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            #region Read Configuration
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);
            IConfiguration config = builder.Build();

            var openaiAzureKey = config["OPENAI_AZURE_KEY"];
            if (string.IsNullOrEmpty(openaiAzureKey))
            {
                Console.WriteLine("Azure OpenAI key not found in configuration");
                return;
            }

            var openaiEndpoint = config["OPENAI_ENDPOINT"];
            if (string.IsNullOrEmpty(openaiEndpoint))
            {
                Console.WriteLine("OpenAI endpoint not found in configuration");
                return;
            }
            #endregion

            try
            {
#pragma warning disable SKEXP0003, SKEXP0011, SKEXP0026, SKEXP0050, SKEXP0052, SKEXP0055
                IKernelBuilder kernelBuilder = Kernel.CreateBuilder()
                            //.WithLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))
                            .AddAzureOpenAIChatCompletion(
                                                     "gpt4", // Azure OpenAI Deployment Name
                                                     openaiEndpoint,
                                                     openaiAzureKey
                                                     );
                kernelBuilder.Services.AddLogging(c => c.AddConsole());
                kernelBuilder.Plugins.AddFromType<LightPlugin>();
                kernelBuilder.Plugins.AddFromType<TimePlugin>();
                Kernel kernel = kernelBuilder.Build();

                

                // Enable auto function calling
                OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                };

                FunctionResult _res = await kernel.InvokePromptAsync(Console.ReadLine()!);
                Console.WriteLine(_res.GetValue<string>());

                var textGenerationService = kernel.GetRequiredService<ITextGenerationService>();
                const string promptTemplate = @"
                    Today is: {{TimePlugin.Date}}
                    Look at the text below: {{$input}} {{LightPlugin.HowTo}} lights on for plugin?";
                // Invoke the kernel with a templated prompt that invokes a plugin and display the result
                FunctionResult result = await kernel.InvokePromptAsync(promptTemplate);
                await Console.Out.WriteLineAsync(result.GetValue<string>());

                // Volatile Memory Store - an in-memory store that is not persisted
                //IMemoryStore store = new VolatileMemoryStore();
                var qdClient = new QdrantVectorDbClient("http://localhost:6333", 1536);

                string input = "What is an amphibian?";
                string[] examples =
                {
                    "What is an amphibian?",
                    "Cos'è un anfibio?",
                    "A frog is an amphibian.",
                    "Frogs, toads, and salamanders are all examples.",
                    "Amphibians are four-limbed and ectothermic vertebrates of the class Amphibia.",
                    "They are four-limbed and ectothermic vertebrates.",
                    "A frog is green.",
                    "A tree is green.",
                    "It's not easy bein' green.",
                    "A dog is a mammal.",
                    "A dog is a man's best friend.",
                    "You ain't never had a friend like me.",
                    "Rachel, Monica, Phoebe, Joey, Chandler, Ross",
                };

                //TextChunker.SplitPlainTextLines();

                var embeddingGen = new AzureOpenAITextEmbeddingGenerationService("ada2",
                                                                                openaiEndpoint,
                                                                                openaiAzureKey);
                
                ReadOnlyMemory<float> inputEmbedding = (await embeddingGen.GenerateEmbeddingsAsync(new[] { input }))[0];
                IList<ReadOnlyMemory<float>> exampleEmbeddings = await embeddingGen.GenerateEmbeddingsAsync(examples);

                float[] similarity = exampleEmbeddings.Select(e => CosineSimilarity(e.Span, inputEmbedding.Span)).ToArray();


                static float CosineSimilarity(ReadOnlySpan<float> x, ReadOnlySpan<float> y)
                {
                    float dot = 0, xSumSquared = 0, ySumSquared = 0;

                    for (int i = 0; i < x.Length; i++)
                    {
                        dot += x[i] * y[i];
                        xSumSquared += x[i] * x[i];
                        ySumSquared += y[i] * y[i];
                    }

                    return dot / (MathF.Sqrt(xSumSquared) * MathF.Sqrt(ySumSquared));
                }

                ISemanticTextMemory memory = new MemoryBuilder()
                                .WithLoggerFactory(kernel.LoggerFactory)
                                .WithAzureOpenAITextEmbeddingGeneration("ada2",
                                                                    openaiEndpoint,
                                                                    openaiAzureKey)
                                //.WithMemoryStore(store);
                                .WithMemoryStore(new QdrantMemoryStore(qdClient))
                                .Build();
                var functions = kernel.Plugins.GetFunctionsMetadata();

                // Check my plugin
                var func = kernel.Plugins["LightPlugin"]["GetState"];
                result = await kernel.InvokeAsync(func);
                Console.WriteLine($"The state of the lights is {result.GetValue<string>()} ");

                // Check TextMemoryPlugin
                // Import the TextMemoryPlugin into the Kernel for other functions
                var memoryPlugin = kernel.ImportPluginFromObject(new TextMemoryPlugin(memory));
                functions = kernel.Plugins.GetFunctionsMetadata();
                const string MemoryCollectionName = "aboutMe";

                //
                // 'Save' may be accomplished or as ISemanticTextMemory.SaveInformationAsync()
                //
                await memory.SaveInformationAsync(MemoryCollectionName,
                    id: "info1",
                    text: "My name is Andrea");
                await memory.SaveInformationAsync(MemoryCollectionName,
                     id: "info2",
                     text: "I work as a tourist operator");
                await memory.SaveInformationAsync(MemoryCollectionName,
                                                id: "info3",
                                                text: "I've been living in Seattle since 2005");

                //
                // Or as direct invocation of the corresponding method
                //
                await kernel.InvokeAsync(memoryPlugin["Save"], new()
                {
                    [TextMemoryPlugin.InputParam] = "My family is from New York",
                    [TextMemoryPlugin.CollectionParam] = MemoryCollectionName,
                    [TextMemoryPlugin.KeyParam] = "info5",
                });

                // 'Get'(Retrieve) may also be accomplished by 2 ways
                // 1. Generaic call to 'Get' within SemanticTextMemory
                MemoryQueryResult? item = await memory.GetAsync(MemoryCollectionName, "info5");
                // or caliing specific function with kernel in plugin
                result = await kernel.InvokeAsync(memoryPlugin["Retrieve"], new KernelArguments()
                {
                    [TextMemoryPlugin.CollectionParam] = MemoryCollectionName,
                    [TextMemoryPlugin.KeyParam] = "info5"
                });

                //
                // 2 ways of SearchSimilarity (Recall) invocation
                //
                //  Uses AI Embeddings for fuzzy lookup of memories based on intent, rather than a specific key.
                IAsyncEnumerable<MemoryQueryResult> answers = memory.SearchAsync(MemoryCollectionName,
                                 query: "where did I grow up?",
                                 minRelevanceScore: 0.79,
                                 withEmbeddings: true,
                                 limit: 2);
                await foreach (MemoryQueryResult answer in answers)
                {
                    Console.WriteLine($"Answer: {answer.Metadata.Text}, Relevance: {answer.Relevance}");
                }

                // Recall (similarity search) with Kernel and TextMemoryPlugin 'Recall' function
                result = await kernel.InvokeAsync(memoryPlugin["Recall"], new()
                {
                    [TextMemoryPlugin.InputParam] = "Ask: where do I live?",
                    [TextMemoryPlugin.CollectionParam] = MemoryCollectionName,
                    [TextMemoryPlugin.LimitParam] = "2",
                    [TextMemoryPlugin.RelevanceParam] = "0.79",
                });

                Console.WriteLine($"Answer: {result.GetValue<string>()}");

            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

#pragma warning restore SKEXP0003, SKEXP0011, SKEXP0026, SKEXP0050, SKEXP0052, SKEXP0055
        }
    }
}
