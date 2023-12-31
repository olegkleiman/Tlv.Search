using Azure.AI.OpenAI;
using BenchmarkDotNet.Running;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace SKCharDrive
{
    internal class Program
    {
        static async Task Main(string[] args) //=> BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        {
            #region Read Configuration

            var confBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);
            IConfiguration config = confBuilder.Build();

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
            #endregion

            // Initialize the kernel
            IKernelBuilder kernelbuilder = Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion("gpt4", // Azure OpenAI Deployment Name,
                                             openaiEndpoint, openaiAzureKey);
            kernelbuilder.Services.AddLogging(c => c.AddConsole());
            var kernel = kernelbuilder.Build();

#pragma warning disable SKEXP0003, SKEXP0011, SKEXP0026, SKEXP0050, SKEXP0052, SKEXP0055

            ISemanticTextMemory memory = new MemoryBuilder()
                                         .WithLoggerFactory(kernel.LoggerFactory)
                                         .WithAzureOpenAITextEmbeddingGeneration("ada2",
                                                                    openaiEndpoint,
                                                                    openaiAzureKey)
                                         .WithQdrantMemoryStore("http://localhost:6333", 1536)
                                         .Build();

            string collectionName = "net7perf";

            var collections = await memory.GetCollectionsAsync();
            if (!collections.Contains(collectionName))
            {
                using (HttpClient client = new())
                {
                    string _clearText = await client.GetStringAsync("https://devblogs.microsoft.com/dotnet/performance_improvements_in_net_7");

                    // Load the entire page into HAP
                    HtmlDocument htmlDoc = new();
                    htmlDoc.LoadHtml(_clearText);
                    HtmlNode? htmlNode = htmlDoc.DocumentNode.SelectSingleNode(".//article");
                    _clearText = htmlNode.InnerText;
                    _clearText = Regex.Replace(_clearText, @"\r\n?|\n", string.Empty);
                    _clearText = WebUtility.HtmlDecode(_clearText);

                    List<string> plainLines = TextChunker.SplitPlainTextLines(_clearText, 128);
                    List<string> paragraphs = TextChunker.SplitPlainTextParagraphs(plainLines, 1024);

                    for (int i = 0; i < 20;
                        //paragraphs.Count; 
                        i++)
                    {
                        await memory.SaveInformationAsync(collectionName, paragraphs[i], $"paragraph{i}");
                    }
                }
            }

#pragma warning restore SKEXP0003, SKEXP0011, SKEXP0026, SKEXP0050, SKEXP0052, SKEXP0055

            // Create a new chat
            IChatCompletionService ai = kernel.GetRequiredService<IChatCompletionService>();
            
            ChatHistory history = [];

            StringBuilder builder = new();

            OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                Temperature = 0
            };

            // Q&A loop
            while (true)
            {
                Console.Write("User > ");
                var question = Console.ReadLine()!;

                var searchResults = memory.SearchAsync(collectionName, question, limit: 1);
                await foreach (var _result in searchResults)
                    builder.AppendLine(_result.Metadata.Text);
                if (builder.Length > 0)
                {
                    builder.Insert(0, "Here's some additional infomation: ");
                    history.AddUserMessage(builder.ToString());
                }
                history.AddUserMessage(question);

                builder.Clear();

                var result = await ai.GetChatMessageContentAsync(history, 
                                                                 executionSettings: openAIPromptExecutionSettings);
                Console.WriteLine($"Assistant > {result}");
                history.AddMessage(result.Role, result.Content);
            }
        }
    }
}
