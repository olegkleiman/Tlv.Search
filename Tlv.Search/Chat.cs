using Ardalis.GuardClauses;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Abstractions;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;
using Tlv.Search.Services;

namespace Tlv.Search
{
#pragma warning disable SKEXP0001, SKEXP0003    
    public class Chat
    {
        private readonly TelemetryClient _telemetryClient;
        private readonly IChatCompletionService _chat;
        private readonly SearchService _searchService;
        private ChatHistory? _chatHistory;

        public Chat(ILoggerFactory loggerFactory,
                    Kernel kernel,
                    SearchService searchService)
        {
            _telemetryClient = new TelemetryClient();
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

                string correlationId = Guid.NewGuid().ToString();

                // Set the correlation identifier in the operation context
                _telemetryClient.Context.Operation.Id = correlationId;
                _telemetryClient?.TrackEvent($"StartChatSearching");

                string errorMessage = Resource.no_history;

                string? historyJson = req.Query["h"];//implicit cast to string?
                historyJson ??= "[]";

             //   _telemetryClient?.TrackTrace($"The content of the chat history" , new Dictionary<string,string> { { "historyJson" , historyJson } , { "prompt" , prompt } });

                ChatMessageContent[]? history = System.Text.Json.JsonSerializer.Deserialize<ChatMessageContent[]>(historyJson);
                history.ToList().ForEach(result =>
                {
                    string jsonResult = JsonConvert.SerializeObject(result);
                    _telemetryClient?.TrackEvent("HistoryChatContent", new Dictionary<string, string> { { "result", jsonResult } });
                });
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

                    var searchResuls = await _searchService.Search(prompt);

                    searchResuls.ForEach(result =>
                    {
                        string jsonResult = JsonConvert.SerializeObject(result);
                        _telemetryClient?.TrackEvent($"SearchResuls", new Dictionary<string, string> { { "result", jsonResult } });
                    });
                  

                    StringBuilder sb = new("Based on the following information:\n\n");
                    foreach (var item in searchResuls)
                    {
                        sb.Append($"{item.summary}\n\n");
                    }
                    sb.Append($"What insights can be drawn about:\n\n{prompt}.\n\nAnswer in Hebrew.");

                    string chatMessage = sb.ToString();

                    _telemetryClient?.TrackTrace($"The content of the answer returned from the chat", new Dictionary<string, string> { { "chatMessage",chatMessage } });

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
                StringBuilder sb1 = new(" ");
             
                 
                
                await foreach (StreamingChatMessageContent content in streamingResult)
                {
                    if (content.Content is not null)
                    {
                        sb1.Append(content);
                        var line = System.Text.Json.JsonSerializer.Serialize(content);

                        await response.WriteAsync($"data: {line}\n\n");
                        await response.Body.FlushAsync();
                        await Task.Delay(30);
                    }
                }
                _telemetryClient?.TrackEvent($"ChatMessageContent", new Dictionary<string, string> { { "ChatMessageContent", sb1.ToString() } });
                await response.WriteAsync("[DONE]");
                await response.Body.FlushAsync();
                _telemetryClient?.TrackEvent($"EndChatSearching");
                _telemetryClient?.TrackTrace($"The search process {correlationId} is over");
            }
            catch (Exception ex)
            {
                _telemetryClient?.TrackException(ex, new Dictionary<string, string> { { $"Contact to perform the search from {nameof(Search)} with error:", ex.Message } });
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
