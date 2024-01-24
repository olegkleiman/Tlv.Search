using Ardalis.GuardClauses;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text;
using System.Text.Json;
using Tlv.Search.Services;

namespace Tlv.Search
{
#pragma warning disable SKEXP0001, SKEXP0003    
    public class Chat
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
        public async Task Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            try
            {

                #region Read Query Parameters

                string? prompt = req.Query["q"];
                Guard.Against.NullOrEmpty(prompt, Resource.no_prompt);

                _logger?.LogInformation($"Executing {nameof(Chat)} with prompt {prompt}");

                string errorMessage = Resource.no_history;

                string? historyJson = req.Query["h"];//implicit cast to string?
                historyJson ??= "[]";

                ChatMessageContent[]? history = JsonSerializer.Deserialize<ChatMessageContent[]>(historyJson);
                Guard.Against.Null(history, errorMessage);

                _chatHistory = new ChatHistory(history);

                #endregion

                Guard.Against.Null(_chat);

                var response = req.HttpContext.Response;
                response.Headers.Append("Content-Type", "text/event-stream");

                OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = 0,
                    MaxTokens = 1000
                    //StopSequences
                };

                // Create a history with a system message
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

                    var searchResuls = await _searchService.Search(prompt, limit: 1, _logger);

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

                await foreach (StreamingChatMessageContent content in streamingResult)
                {
                    if (content.Content is not null)
                    {
                        var line = JsonSerializer.Serialize(content);
                        await response.WriteAsync($"data: {line}\n\n");
                        await response.Body.FlushAsync();
                        await Task.Delay(30);
                    }
                }

                await response.WriteAsync("[DONE]");
                await response.Body.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.Message);
            }

        }
        private async Task<string> GetMessage(int n)
        {
            await Task.Delay(10);
            return $"data: Message1 #{n}\n\n";
        }

        async IAsyncEnumerable<string> GetMessages(int max)
        {
            for (var i = 1; i <= max; i++)
            {
                var message = await GetMessage(i);
                yield return message;
            }
        }
    }
#pragma warning restore SKEXP0001, SKEXP0003
}
