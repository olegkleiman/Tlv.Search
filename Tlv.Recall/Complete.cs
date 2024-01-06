using System.Net;
using System.Text.Json;
using System.Web.Http;
using Ardalis.GuardClauses;
using Azure.AI.OpenAI;
using EmbeddingEngine.Core;
using Json.Schema.Generation.Intents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using static System.Net.Mime.MediaTypeNames;
using HttpTriggerAttribute = Microsoft.Azure.Functions.Worker.HttpTriggerAttribute;
using QueueTriggerAttribute = Microsoft.Azure.Functions.Worker.QueueTriggerAttribute;

namespace Tlv.Recall
{
    public class Complete : SearchBase
    {
        private readonly ILogger _logger;
        private readonly IChatCompletionService _chat;
        private readonly ChatHistory _chatHistory;

        public Complete(ILoggerFactory loggerFactory,
                        Kernel kernel)
                        //ChatHistory chatHistory,
                        //IChatCompletionService chat)
        {
            _logger = loggerFactory.CreateLogger<Complete>();
            _chat = kernel.GetRequiredService<IChatCompletionService>();
            _chatHistory = new ChatHistory();
           }

        [Function(nameof(Complete))]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            try
            {
                string? prompt = req.Query["q"];
                if (string.IsNullOrEmpty(prompt))
                {
                    var _response = req.CreateResponse(HttpStatusCode.BadRequest);
                    await _response.WriteStringAsync("Please provide some input, i.e. add ?q=... to invocation url");
                    return _response;
                }

                _logger.LogInformation($"Executing {nameof(Complete)} with prompt {prompt}");

                #region Read Configuration

                string? embeddingsProviderName = req.Query["p"] ?? "OPENAI";
                string? openaiEndpoint = GetConfigValue("OPENAI_ENDPOINT");
                string? openaiAzureKey = GetConfigValue("OPENAI_AZURE_KEY");
                string? collectionName = GetConfigValue("COLLECTION_NAME");
                string? vectorDbProviderKey = GetConfigValue("VECTOR_DB_PROVIDER_KEY");
                string? configKeyName = $"{embeddingsProviderName.ToString().ToUpper()}_KEY";

                configKeyName = $"{embeddingsProviderName.ToString().ToUpper()}_AZURE_KEY";
                string? azureApiKey = GetConfigValue(configKeyName);

                #endregion

                Guard.Against.Null(_chat);

                OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = 0,
                    MaxTokens = 100
                };

                _chatHistory.AddUserMessage($"Classify the following user input as either a question or a statement:\n\n\"{prompt}\"");
                ChatMessageContent result = await _chat.GetChatMessageContentAsync(_chatHistory,
                                                                        executionSettings: openAIPromptExecutionSettings);
                string errorMessage = "Couldn't classify user's chat message";
                Guard.Against.Null(result, string.Empty, errorMessage);
                if( result is null 
                    || result.Content is null )
                    throw new ApplicationException(errorMessage);

                _chatHistory.Clear();

                if ( result.Content.Contains("question", StringComparison.OrdinalIgnoreCase) )
                {
                    
                    var searchResuls = await Search(openaiAzureKey,
                                                openaiEndpoint,
                                                collectionName,
                                                prompt);

                    if (searchResuls.Count > 0)
                    {
                        string? summary = searchResuls[0].summary;
                        if (!string.IsNullOrEmpty(summary))
                            _chatHistory.AddUserMessage($"Based on the following information:\n\n{summary}\n\nWhat insights can we draw about:\n\n{prompt}");
                    }
                }
                
                IAsyncEnumerable<StreamingChatMessageContent>
                    streamingResult = _chat.GetStreamingChatMessageContentsAsync(_chatHistory,
                                                        executionSettings: openAIPromptExecutionSettings);

                HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);

                StreamWriter sw = new(response.Body);
                await foreach (StreamingChatMessageContent line in streamingResult)
                {
                    var _line = JsonSerializer.Serialize(line);
                    sw.WriteLine(_line);
                }
                sw.Flush();
                response.Body.Position = 0;

                //await response.WriteAsJsonAsync(_result);
                return response;
            }
            catch (Exception ex)
            {
                var _response = req.CreateResponse(HttpStatusCode.InternalServerError);
                _response.WriteString(ex.Message);
                return _response;
            }
        }
    }
}
