﻿using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.IO;
using Ardalis.GuardClauses;
using EmbeddingEngine.Core;
using System.Linq;

public class Complete : SearchBase
{

    [FunctionName("complete")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
        ILogger log)
    {
        string question = req.Query["q"];
        if (string.IsNullOrEmpty(question))
        {
            return new BadRequestObjectResult("Question is required");
        }

        string historyJson = req.Query["h"].ToString() ?? "[]";
        Message[] history = JsonConvert.DeserializeObject<Message[]>(historyJson);
        if (history == null)
        {
            return new BadRequestObjectResult("History must be array type");
        }

        try
        {
            #region Read Configuration

            string? collectionName = GetConfigValue("QDRANT_COLLECTION_NAME");
            string? qrantHostName = GetConfigValue("QDRANT_HOST_NAME");
            string? qgrantKey = GetConfigValue("QDRANT_KEY");

            string? embeddingsProviderName = "OPENAI";
            EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), embeddingsProviderName);

            string configKeyName = $"{embeddingsProviderName.ToUpper()}_KEY";
            string? providerKey = GetConfigValue(configKeyName);
            Guard.Against.NullOrEmpty(providerKey, configKeyName, $"Couldn't find {configKeyName} in configuration");

            #endregion

            OpenAiService _openAiService = new OpenAiService(providerKey);

            //var processedQ = await _openAiService.ProcessUserInput(question, history);
            var intent = await _openAiService.DetermineIntent(question);
            if (intent == "database_query")
            {
                var processedQ = await _openAiService.ProcessUserInput(question, history);
                var results = await Search(embeddingsProviderName, providerKey, qrantHostName, qgrantKey, collectionName, processedQ);
                var ctx = results[0].summary;
                question = await _openAiService.FrameQuestion(results.Select(p => p.summary).ToList(), processedQ);
            }


            var responseStream = await StreamOpenAI(question, history, providerKey);
            return new OkObjectResult(responseStream);
        }
        catch (Exception ex)
        {
            log.LogError($"Exception: {ex.Message}");
            return new StatusCodeResult(500);
        }
    }

   private static async Task<Stream> StreamOpenAI(string message, 
                                                   Message[] history,
                                                   string providerKey)
    {
        using (HttpClient httpClient = new HttpClient())
        {
            httpClient.DefaultRequestHeaders.Authorization = new("Bearer", providerKey);

            var combinedList = new List<Message>
            {
                new Message { role = "system", content = "אתה עוזר בשפה העברית" }
            };
            combinedList.AddRange(history);

            combinedList.Add(new Message { role = "user", content = $"{message ?? "תאמר שלום"}" });

            var content = new
            {
                model = "gpt-3.5-turbo",
                messages = combinedList,
                temperature = 0,
                max_tokens = 1000,
                n = 1,
                stream = true
            };

            var stringContent = JsonConvert.SerializeObject(content);
            var requestContent = new StringContent(stringContent, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage()
            {
                Content = requestContent,
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://api.openai.com/v1/chat/completions")
            };
            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            //var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", requestContent);

            var stream = await response.Content.ReadAsStreamAsync();
            
            return stream;
        }

    }

}
