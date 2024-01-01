using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
//using Microsoft.Extensions.Logging;
//using Newtonsoft.Json;
using Qdrant.Client;
using Ardalis.GuardClauses;
using System.Net;
using Tlv.Search.models;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using System.Collections.Generic;
using System.Collections;
using Qdrant.Client.Grpc;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Logging;

namespace Tlv.Search
{
    // Gemini-related stuff
    class Text
    {
        public string text { get; set; }
    }
    class Content
    {
        public Text[] parts { get; set; }
    }
    class GeminiPayload
    {
        public string model { get; set; }
        public Content content { get; set; }
    }
    class Values
    {
        public float[]? values { get; set; }
    }

    class GeminiResponse
    {
        public Values? embedding { get; set; }
    }

    public class Search3
    {
        private readonly ILogger<Search3>? _logger;

        public Search3(ILogger<Search3> log)
        {
            _logger = log;
        }

        private async Task<Single[]?> GetEmbeddings(string embeddingEngineKey,
                                                     string input)
        {
            using HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json; charset=utf-8");
            Text[] _parts = new Text[1];
            _parts[0] = new Text()
            {
                text = input
            };
            var payload = new GeminiPayload
            {
                model = "models/embedding-001",
                content = new Content()
                {
                    parts = _parts
                }
            };
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/embedding-001:embedContent?key={embeddingEngineKey}";
            HttpResponseMessage response = await httpClient.PostAsJsonAsync(url, payload);
            response.EnsureSuccessStatusCode();

            string respContent = await response.Content.ReadAsStringAsync();
            GeminiResponse? geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(respContent);
            return geminiResponse?.embedding?.values;
        }

        [FunctionName(nameof(Search3))]
        [OpenApiOperation(operationId: "Run", tags: new[] { "q" })]
        [OpenApiParameter(name: "q", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **prompt** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req)
        {
            string? prompt = req.Query["q"];
            if (string.IsNullOrEmpty(prompt))
                return new BadRequestObjectResult("Please provide some input");

            _logger?.LogInformation($"Running search3 with prompt '{prompt}'");

            string configKey = "QDRANT_HOST";
            string? qDrantHost = Environment.GetEnvironmentVariable(configKey);
            Guard.Against.NullOrEmpty(qDrantHost, configKey, $"Couldn't find {configKey} in configuration");

            configKey = "GEMINI_KEY";
            string? embeddingEngineKey = Environment.GetEnvironmentVariable(configKey);
            Guard.Against.NullOrEmpty(embeddingEngineKey, configKey, $"Couldn't find {configKey} in configuration");

            QdrantClient qdClient = new(qDrantHost);

            string collectionName = "doc_parts";
            SearchParams sp = new()
            {
                Exact = false
            };

            // Get embeddings from Gemini
            float[]? embeddings = await GetEmbeddings(embeddingEngineKey, prompt);
            Guard.Against.Null(embeddings);

            var scores = await qdClient.SearchAsync(collectionName, embeddings,
                                                    searchParams: sp,
                                                    limit: 5);

            List<SearchItem>? searchItems = new();
            foreach (var score in scores)
            {
                var payload = score.Payload;

                SearchItem si = new()
                {
                    id = score.Id.Num,
                    title = payload["title"].StringValue,
                    summary = payload["text"].StringValue,
                    url = payload["url"].StringValue,
                    imageUrl = payload["image_url"].StringValue,
                    similarity = score.Score
                };

                searchItems.Add(si);
            }
            return new JsonResult(searchItems)
            {
                StatusCode = (int)HttpStatusCode.OK,
                ContentType = "application/json"
            };
        }
    }
}
