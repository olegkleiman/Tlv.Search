using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using System.Net;
using Qdrant.Client;
using System.Linq;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;
using System.Collections;
using System.Collections.Generic;
using Azure.AI.OpenAI;
using Microsoft.Identity.Client;
using Azure;
using System.Web.Http;
using Ardalis.GuardClauses;
using Tlv.Search.models;
using Google.Protobuf.Collections;

namespace Tlv.Search
{
    public class Search2
    {
        private readonly ILogger<Search>? _logger;

        public Search2(ILogger<Search> log)
        {
            _logger = log;
        }



        [FunctionName(nameof(Search2))]
        [OpenApiOperation(operationId: "Run", tags: new[] { "q" })]
        [OpenApiParameter(name: "q", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **prompt** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            string? prompt = req.Query["q"];
            if( _logger is not null )
                _logger.LogInformation($"Running search with prompt '{prompt}'");

            try
            {
                string? qDrantHost = Environment.GetEnvironmentVariable("QDRANT_HOST");
                Guard.Against.NullOrEmpty(qDrantHost, "QDRANT_HOST", "Couldn't find QDRANT_HOST in configuration");

                string ? collectionName = Environment.GetEnvironmentVariable("QDRANT_COLLECTOIN_NAME");
                Guard.Against.NullOrEmpty(collectionName, "QDRANT_COLLECTOIN_NAME", "Couldn't find QDRANT_COLLECTOIN_NAME in configuration");

                string ? vectorSize = Environment.GetEnvironmentVariable("QDRANT_VECTOR_SIZE");
                Guard.Against.NullOrEmpty(vectorSize, "VECTOR_SIZE", "Couldn't find VECTOR_SIZE in configuration");
                ulong uVectorSize = ulong.Parse(vectorSize);

                string? providerKey = Environment.GetEnvironmentVariable("OPENAI_KEY");
                Guard.Against.NullOrEmpty(providerKey, "OPENAI_KEY", "No OpenAI Key found in configuration");

                QdrantClient qdClient = new(qDrantHost);
                var collections = await qdClient.ListCollectionsAsync();
                var q = (from collection in collections
                         where collection == collectionName
                         select collection).FirstOrDefault();
                if (q == null)
                {
                    VectorParams vp = new()
                    {
                        Distance = Distance.Cosine,
                        Size = uVectorSize
                    };

                    await qdClient.CreateCollectionAsync(collectionName, vp);
                }

                //
                // Search
                //
                OpenAIClient client = new(providerKey);


                //SearchParams sp = new SearchParams()
                //{
                //    Exact = true
                    
                //};

                List<float> queryVector = new();
                List<string> prompts = new()
                {
                    prompt
                };
                EmbeddingsOptions eo = new(deploymentName: "text-embedding-ada-002",
                                           input: prompts);

                Response<Embeddings> response = await client.GetEmbeddingsAsync(eo);
                foreach (var item in response.Value.Data)
                {
                    var embedding = item.Embedding;

                    for (int i = 0; i < embedding.Length; i++)
                    {
                        float value = embedding.Span[i];
                        queryVector.Add(value);
                    }
                }

                //
                // first step of search - search in all the documents in general collection
                //
                var scores = await qdClient.SearchAsync(collectionName, queryVector.ToArray(), limit: 5);
                List<SearchItem>? searchItems = new();
                foreach (var score in scores)
                {
                    var payload = score.Payload;
                    ulong docId = score.Id.Num;
                    
                    //
                    // second search in sub-documents
                    //
                    string subCollectionName = $"doc_parts";

                    FieldCondition fc = new FieldCondition()
                    {
                        //Match = new Match()
                        //{
                        //    Keyword = "fff",
                        //    Text
                        //}
                        Key = "parent_doc_id",
                        Range = new Qdrant.Client.Grpc.Range()
                        {
                            Gte = docId
                        }
                    };

                    //Filter _filter = new Filter()
                    //{
                    //    Must = null
                    //};

                    Qdrant.Client.Grpc.Range range = new Qdrant.Client.Grpc.Range { Gte = docId };
                    Filter filter = Range("parent_doc_id", range);
                    var subScores = await qdClient.SearchAsync(subCollectionName, queryVector.ToArray(), 
                                                                filter: filter,
                                                                limit:2);
                    ScoredPoint scoredPoint = subScores[0]; // TBD
                    var subPayload = scoredPoint.Payload;
                    var summary = subPayload["text"];

                    searchItems.Add(new SearchItem()
                    {
                        id = score.Id.Num,
                        title = payload["title"].StringValue,
                        summary = summary.HasStringValue ? summary.StringValue : string.Empty,
                        url = payload["url"].StringValue,
                        imageUrl = payload["image_url"].StringValue,
                        similarity = score.Score
                    });
                }

                return new JsonResult(searchItems)
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    ContentType = "application/json"
                };
            }
            catch (ArgumentException ex)
            {
                // This is equivalent to InternalServerErrorResult, but with the message
                return new ObjectResult(new { error = ex.Message })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
            return new OkObjectResult("ok");
        }
    }
}
