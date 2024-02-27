using Ardalis.GuardClauses;
using EmbeddingEngine.Core;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using System.Net;
using Tlv.Search.Common;
using Tlv.Search.Models;
using Tlv.Search.Services;
using VectorDb.Core;

namespace Tlv.Search
{
    public class Search
    {
        private readonly TelemetryClient _telemetryClient;
        private readonly IPromptProcessingService? _promptService;
        private readonly SearchService _searchService;

        public Search(TelemetryClient telemetryClient,
                      IPromptProcessingService promptService,
                      SearchService searchService)
        {
            _telemetryClient = telemetryClient ;
            _promptService = promptService;

            _searchService = searchService;
        }
        
        protected string? GetConfigValue(string configKey)
        {
            string? value = Environment.GetEnvironmentVariable(configKey);
            Guard.Against.NullOrEmpty(value, configKey, $"Couldn't find '{configKey}' in configuration");

            return value;
        }

        [Function(nameof(Search))]
        [OpenApiOperation(operationId: "Run", tags: new[] { "q" })]
        [OpenApiParameter(name: "q", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **prompt** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
                                             ILogger logger)
        {
            Dictionary<string, string> searchParameters = new Dictionary<string, string>();
            try
            {
               
                string? prompt = req.Query["q"];
                if (string.IsNullOrEmpty(prompt))
                    return new BadRequestObjectResult("Please provide some input, i.e. add ?q=... to invocation url");
                string correlationId = Guid.NewGuid().ToString();
                
                // Set the correlation identifier in the operation context
                _telemetryClient.Context.Operation.Id = correlationId;
                _telemetryClient?.TrackTrace($"Start searching");
                searchParameters.Add("prompt", prompt);

                PromptContext? promptContext = await _promptService?.CreateContext(prompt);
                searchParameters.Add("filtered_prompt", promptContext.FilteredPrompt);
                var searchResults = await _searchService.Search(promptContext, limit: 5, logger);
                int index = 0;
                searchResults.ForEach(result =>
                {
                    string jsonResult = JsonConvert.SerializeObject(result);
                    searchParameters.Add($"result{++index}", jsonResult);
                   _telemetryClient?.TrackTrace($"Search results:" , new Dictionary<string,string> { { "result", jsonResult } });
                });
                _telemetryClient?.TrackEvent("Search", searchParameters);
                _telemetryClient?.TrackTrace($"The search process {correlationId} finished");

                return new OkObjectResult(searchResults);
            }
            catch (Exception ex)
            {
                searchParameters.Add("exception", ex.Message);
                _telemetryClient?.TrackEvent("Search", searchParameters);
                _telemetryClient?.TrackException(ex, searchParameters);

                return new ObjectResult(new { ex.Message })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }
    }
}
