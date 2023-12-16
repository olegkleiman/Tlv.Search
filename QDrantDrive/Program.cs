﻿using Azure;
using Azure.AI.OpenAI;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
//using Microsoft.SemanticKernel.CoreSkills;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Microsoft.SemanticKernel.Plugins.Core;
using System;
using Google.Protobuf;

namespace QDrantDrive
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                List<string> prompts = ["Hello", "from QDrant"];

                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false);
                IConfiguration config = builder.Build();

                var openaiKey = config["OPENAI_KEY"];
                var openaiEndpoint = config["OPENAI_ENDPOINT"];
                var vectorSize = ulong.Parse(config["VECTOR_SIZE"]);
                var client = new OpenAIClient(openaiKey, new OpenAIClientOptions());

                try
                {
                    ILoggerFactory myLoggerFactory = NullLoggerFactory.Instance;

                    var kernelBuilder = Kernel.CreateBuilder();
                    //kernelBuilder.Services. .AddSingleton(myLoggerFactory);

#pragma warning disable SKEXP0011, SKEXP0050, SKEXP0052
                    kernelBuilder.AddAzureOpenAITextEmbeddingGeneration("text-embedding-ada-002", client);
                    var kernel = kernelBuilder.Build();
                    //kernel.AddFromType<TimePlugin>();

                    var memoryBuilder = new MemoryBuilder();
                    memoryBuilder.WithAzureOpenAITextEmbeddingGeneration("text-embedding-ada-002", "model-id", openaiEndpoint, openaiKey);

#pragma warning restore SKEXP0011, SKEXP0050, SKEXP0052

                    var func = kernel.CreateFunctionFromPrompt(prompts[0]);
                    var res = await kernel.InvokeAsync(func);
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                string collectionName = "site_docs";

                QdrantClient qdClient = new("localhost");
                var collections =  await qdClient.ListCollectionsAsync();
                var q = (from collection in collections
                        where collection == collectionName
                        select collection).FirstOrDefault();
                if (q == null)
                {
                    VectorParams vp = new()
                    {
                        Distance = Distance.Cosine,
                        Size = vectorSize
                    };

                    await qdClient.CreateCollectionAsync(collectionName, vp);
                }

                
                EmbeddingsOptions eo = new()
                {
                    DeploymentName = "text-embedding-ada-002",
                    Input = prompts
                };
                Response<Embeddings> response = await client.GetEmbeddingsAsync(eo);
                List<PointStruct> points = [];
                var random = new Random();
                foreach (var item in response.Value.Data)
                {
                    var embedding = item.Embedding;
                    int itemIndex = item.Index;

                    List<float> _vectors = [];

                    for (int i = 0; i < embedding.Length; i++)
                    {
                        float value = embedding.Span[i];
                        _vectors.Add(value);
                    }
                    PointStruct ps = new()
                    {
                        Id = (ulong)item.Index,
                        Payload =
                        {
                            ["text"] = prompts[itemIndex],
                            ["title"] = "הנחות מארנונה",
                            ["url"] = "https://www.tel-aviv.gov.il/Residents/Arnona/Pages/ArnonaSwitching.aspx"
                        },
                        Vectors = _vectors.ToArray()
                    };

                    points.Add(ps);

                }

                await qdClient.UpsertAsync(collectionName, points);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
