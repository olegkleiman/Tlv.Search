﻿using System;
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
            if (string.IsNullOrEmpty(prompt))
                return new BadRequestObjectResult("Please provide some input");

            //prompt = " " + prompt;

            if ( _logger is not null )
                _logger.LogInformation($"Running search with prompt '{prompt}'");

            try
            {
                string? qDrantHost = Environment.GetEnvironmentVariable("QDRANT_HOST");
                Guard.Against.NullOrEmpty(qDrantHost, "qDrantHost", "Couldn't find QDRANT_HOST in configuration");

                string ? collectionName = Environment.GetEnvironmentVariable("QDRANT_COLLECTION_NAME");
                Guard.Against.NullOrEmpty(collectionName, collectionName, "Couldn't find QDRANT_COLLECTOIN_NAME in configuration");

                string ? vectorSize = Environment.GetEnvironmentVariable("QDRANT_VECTOR_SIZE");
                Guard.Against.NullOrEmpty(vectorSize, "VECTOR_SIZE", "Couldn't find VECTOR_SIZE in configuration");
                ulong uVectorSize = ulong.Parse(vectorSize);

                string? providerKey = Environment.GetEnvironmentVariable("OPENAI_KEY");
                Guard.Against.NullOrEmpty(providerKey, providerKey, "No OpenAI Key found in configuration");

                QdrantClient qdClient = new(qDrantHost);
                var collections = await qdClient.ListCollectionsAsync();
                var q = (from collection in collections
                         where collection == collectionName
                         select collection).FirstOrDefault();
                if (q == null)
                {
                    // This is equivalent to InternalServerErrorResult, but with the message
                    return new ObjectResult(new { error = $"Couldn't find '{collectionName}' collection" })
                    {
                        StatusCode = StatusCodes.Status500InternalServerError
                    };
                }

                //
                // Search
                //
                OpenAIClient client = new(providerKey);

                SearchParams sp = new()
                {
                    Exact = false
                };

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

                collectionName = "doc_parts";

                var scores = await qdClient.SearchAsync(collectionName, queryVector.ToArray(),
                                            searchParams: sp, limit: 5);

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
                ////
                //// first step of search - search in all the documents in general collection
                ////
                //var scores = await qdClient.SearchAsync(collectionName, queryVector.ToArray(), 
                //                                        searchParams: sp,    
                //                                        limit: 5);
                //List<SearchItem>? searchItems = new();
                //foreach (var score in scores)
                //{
                //    var payload = score.Payload;
                //    ulong docId = score.Id.Num;

                //    SearchItem si = new SearchItem()
                //    {
                //        id = score.Id.Num,
                //        title = payload["title"].StringValue,
                //        summary = string.Empty, // may be set from subDoc below
                //        url = payload["url"].StringValue,
                //        imageUrl = payload["image_url"].StringValue,
                //        similarity = score.Score
                //    };

                //    //
                //    // second search in sub-documents
                //    //
                //    string subCollectionName = "doc_parts";


                //    Qdrant.Client.Grpc.Range range = new Qdrant.Client.Grpc.Range { Gte = docId };
                //    Filter filter = Match("parent_doc_id", (long)docId); //HasId(docId);// // Range("parent_doc_id", range);
                //    var subScores = await qdClient.SearchAsync(subCollectionName, queryVector.ToArray(), 
                //                                                filter: filter,
                //                                                searchParams: sp,
                //                                                limit:1);
                //    if (subScores.Count > 0)
                //    {
                //        ScoredPoint scoredPoint = subScores[0]; // TBD
                //        var subPayload = scoredPoint.Payload;
                //        var summary = subPayload["text"];

                //        if( summary.HasStringValue )
                //            si.summary = summary.StringValue;
                //    }

                //    searchItems.Add(si);
                //}

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
