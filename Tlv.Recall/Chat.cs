using Ardalis.GuardClauses;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Net;
using System.Text;
using System.Text.Json;
using Tlv.Recall.Services;
using HttpTriggerAttribute = Microsoft.Azure.Functions.Worker.HttpTriggerAttribute;

namespace Tlv.Recall
{
    public class Chat // : SearchBase
    {
        private readonly ILogger _logger;
        private readonly IChatCompletionService _chat;
        private readonly SearchService _searchService;
        private ChatHistory? _chatHistory;

        public Chat(ILoggerFactory loggerFactory,
                    Kernel kernel,
                    SearchService searchService)
        {
            _logger = loggerFactory.CreateLogger<Chat>();
            _chat = kernel.GetRequiredService<IChatCompletionService>();
            _searchService = searchService;
            //var context = kernel.Plugins.
        }

        protected static string? GetConfigValue(string configKey)
        {
            string? value = Environment.GetEnvironmentVariable(configKey);
            Guard.Against.NullOrEmpty(value, configKey, $"Couldn't find '{configKey}' in configuration");

            return value;
        }

        /// <summary>
        /// Invokes the chat function to get a response from the bot.
        /// </summary>
        [Function(nameof(Chat))]
        [OpenApiOperation(operationId: "Run", tags: new[] { "q" })]
        [OpenApiParameter(name: "q", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **prompt** parameter")]
        [OpenApiParameter(name: "h", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "chat history as json array")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(string), Description = "The OK response")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            try
            {
                #region Read Query Parameters

                string? prompt = req.Query["q"];
                Guard.Against.NullOrEmpty(prompt, Resource.no_prompt);

                _logger?.LogInformation($"Executing {nameof(Chat)} with prompt {prompt}");

                string errorMessage = Resource.no_history;
                string? historyJson = req.Query["h"] ?? "[]";
                Guard.Against.NullOrEmpty(historyJson, errorMessage);

                ChatMessageContent[]? history = JsonSerializer.Deserialize<ChatMessageContent[]>(historyJson);
                Guard.Against.Null(history, errorMessage);

                _chatHistory = new ChatHistory(history);

                #endregion

                //#region Read Configuration

                //string? embeddingsProviderName = req.Query["p"] ?? "OPENAI";
                //string? openaiEndpoint = GetConfigValue("OPENAI_ENDPOINT");
                //string? openaiAzureKey = GetConfigValue("OPENAI_AZURE_KEY");
                //string? collectionName = GetConfigValue("COLLECTION_NAME");
                //string? vectorDbProviderKey = GetConfigValue("VECTOR_DB_PROVIDER_KEY");

                //#endregion

                Guard.Against.Null(_chat);

                OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = 0,
                    MaxTokens = 1000
                };

                ChatHistory tempChatHistory = new($"Classify the following user input as either a question or a statement:\n\n\"{prompt}\". Answer in English");
                ChatMessageContent result = 
                    await _chat.GetChatMessageContentAsync(tempChatHistory,
                                                           executionSettings: openAIPromptExecutionSettings);
                Guard.Against.Null(result, string.Empty, Resource.no_classification);
                if (result is null || result.Content is null)
                    throw new ApplicationException(Resource.no_classification);

                if (result.Content.Contains("question", StringComparison.OrdinalIgnoreCase))
                {
                    var q = string.Join(" ", from message in _chatHistory
                                             where message.Role == AuthorRole.User
                                             select message.Content);

                    var searchResuls = await _searchService.Search(q, limit: 5);

                    //searchResuls = await Search(openaiAzureKey,
                    //                                openaiEndpoint,
                    //                                collectionName,
                    //                                vectorDbProviderKey,
                    //                                q, //prompt,
                    //                                limit: 5);

                    StringBuilder sb = new("Based on the following information:\n\n");
                    foreach (var item in searchResuls)
                    {
                        sb.Append($"{item.summary}\n\n");
                    }
                    sb.Append($"What insights can be drawn about:\n\n{prompt}.\n\nAnswer in Hebrew.");

                    string chatMessage = sb.ToString();
                    _chatHistory.AddUserMessage(chatMessage);
                }
                else
                {
                    _chatHistory.Clear();
                    _chatHistory.AddUserMessage(prompt);
                }

                IAsyncEnumerable<StreamingChatMessageContent>
                    streamingResult = _chat.GetStreamingChatMessageContentsAsync(_chatHistory,
                                                        executionSettings: openAIPromptExecutionSettings);

                HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);

                StreamWriter sw = new(response.Body);
                await foreach (StreamingChatMessageContent content in streamingResult)
                {
                    if (content.Content is not null)
                    {
                        var _line = JsonSerializer.Serialize(content);
                        sw.WriteLine(_line);
                    }
                }
                sw.Flush();
                response.Body.Position = 0;

                return response;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.Message);

                var _response = req.CreateResponse(HttpStatusCode.InternalServerError);
                _response.WriteString(ex.Message);
                return _response;
            }
        }
    }
}
