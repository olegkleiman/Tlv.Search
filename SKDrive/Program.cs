using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.HuggingFace;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using System.Collections;
using System.Net;
using System.Net.Http.Headers;
//using Microsoft.SemanticKernel.Memory;


namespace SKDrive
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);
            IConfiguration config = builder.Build();

            var openaiKey = config["OPENAI_KEY"];
            if (string.IsNullOrEmpty(openaiKey))
            {
                Console.WriteLine("OpenAI key not found in configuration");
                return;
            }

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

            try
            {
                // Azure OpenAI package
                //var client = new OpenAIClient(openaiKey, new OpenAIClientOptions());

                IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
                kernelBuilder.Services.AddLogging(c => c.AddConsole());
                kernelBuilder.AddOpenAIChatCompletion("gpt-3.5-turbo", openaiKey);
                kernelBuilder.Plugins.AddFromType<LightPlugin>();
                var kernel = kernelBuilder.Build();

//#pragma warning disable SKEXP0001, SKEXP0003, SKEXP0020, SKEXP0026
//                var memoryBuilder = new MemoryBuilder()
//                    .WithLoggerFactory(kernel.LoggerFactory);
//                   // .WithTextEmbeddingGeneration(embeddingService)
//                   //.WithAzureOpenAITextEmbeddingGeneration("ada2",
//                   //                                        openaiEndpoint,
//                   //                                        openaiAzureKey)
//                   //.WithMemoryStore(new QdrantMemoryStore(qdClient));
//                var memory = memoryBuilder.Build();

//                string collectionName = "MyCollection";
//                await memory.SaveInformationAsync(collectionName, "Today is a sunny day and I will get some ice cream.", "1000");

//#pragma warning restore SKEXP0003, SKEXP0020, SKEXP0026

                //                const string MemoryCollectionName = "aboutMe";
                //                await memory.SaveInformationAsync(MemoryCollectionName, id: "info1", text: "My name is Andrea");
                //                await memory.SaveInformationAsync(MemoryCollectionName, id: "info2", text: "My name is Irina");
                //                MemoryQueryResult? lookup  = await memory.GetAsync(MemoryCollectionName, "info2", withEmbedding: true);

                //                IAsyncEnumerable<MemoryQueryResult> searchResults = memory.SearchAsync(MemoryCollectionName, "Irina",
                //                                                                                        limit: 2, minRelevanceScore: 0.5);
                //                await foreach (MemoryQueryResult item in searchResults)
                //                {
                //                    Console.WriteLine(item.Metadata.Text + " : " + item.Relevance);
                //                }

                //#pragma warning restore SKEXP0003, SKEXP0011, SKEXP0026, SKEXP0050, SKEXP0052


                var functions = kernel.Plugins.GetFunctionsMetadata();

                // Invoke the kernel with a chat prompt and display the result
                string chatPrompt = @"
                    <message role=""user"">What is Seattle?</message>
                    <message role=""system"">Respond with JSON.</message>
                ";
                Console.WriteLine(await kernel.InvokePromptAsync(chatPrompt));

                //
                // Chat
                //

                ChatHistory history = [];
                var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();


                /**
                 * Logit_bias is an optional parameter that modifies the likelihood of specified tokens appearing in a Completion.
                 * When using the Token Selection Biases parameter, the bias is added to the logits generated by the model prior to sampling.
                 */

                // To use Logit Bias you will need to know the token ids of the words you want to use.
                // Getting the token ids using the GPT Tokenizer: https://platform.openai.com/tokenizer

                // The following text is the tokenized version of the book related tokens
                // "novel literature reading author library story chapter paperback hardcover ebook publishing fiction nonfiction manuscript textbook bestseller bookstore reading list bookworm"
                var keys = new[] { 3919, 626, 17201, 1300, 25782, 9800, 32016, 13571, 43582, 20189, 1891, 10424, 9631, 16497, 12984, 20020, 24046, 13159, 805, 15817, 5239, 2070, 13466, 32932, 8095, 1351, 25323 };
                
                while ( true )
                {
                    Console.Write("User > ");
                    history.AddUserMessage(Console.ReadLine()!);

                    // Enable auto function calling
                    OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
                    {
                        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                        TokenSelectionBiases = keys.ToDictionary(key => key, key => -100)
                    };

                    // Get the response from the AI
                    var result = await chatCompletionService.GetChatMessageContentAsync(history,
                                                    executionSettings: openAIPromptExecutionSettings,
                                                    kernel: kernel);
                    Console.WriteLine($"Assistant > {result}");

                    history.AddMessage(result.Role, result.Content);
                }
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync(ex.Message);
            }
        }
    }
}
