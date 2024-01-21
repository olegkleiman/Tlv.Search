using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using static OdysseyFunctions.DiscountTypesConverterDTO;
using Azure.AI.OpenAI;
using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Qdrant.Client.Grpc;
using Qdrant.Client;
using System.Web;
using System.Text.RegularExpressions;

namespace OdysseyFunctions
{
    public static class DiscountTypesArnona
    {
        [FunctionName("DiscountTypesArnona")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {


            log.LogInformation("C# HTTP trigger function processed a request.");

            var config = new ConfigurationBuilder()
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
               .AddEnvironmentVariables()
               .Build();
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            // Deserialize the JSON body if needed
            List<DiscountTypesConverterDTO> requestBodyObject = JsonConvert.DeserializeObject<List<DiscountTypesConverterDTO>>(requestBody);
            List<EntityDTO> entityDTOs = new List<EntityDTO>();

            var listOfText = requestBodyObject.SelectMany(x => x.Fields.Where(x=> x.Caption =="Title").Select(x=> x.Value)).ToList();
            var listOfTitle = requestBodyObject.SelectMany(x => x.Fields.Where(x => x.InternalName == "DiscountField").Select(x => x.Value)).ToList();
            var listOfUrl = requestBodyObject.SelectMany(x => x.Fields.Where(x => x.InternalName == "MainItemPreview").Select(x => ExtractHrefValue(HttpUtility.HtmlDecode(x.Value)))).ToList();
            for (int i= 0; i < listOfText.Count  && i< listOfTitle.Count  && i<listOfUrl.Count; i++) 
            {
                entityDTOs.Add(new EntityDTO {Text = listOfText[i] , Title = listOfTitle[i] , Url = config["Url_Page"] + listOfUrl[i]});
            }
            var groupedEntities = entityDTOs
                       .GroupBy(entity => entity.Title)
                       .Select(group =>
                           new
                           {
                               Title = group.Key,
                               Text = string.Join("\r\n", group.Select(entity => entity.Text + "\r\n" + entity.Url)),
                               EmbeddingText = string.Join("   ", group.Select(entity => entity.Text))
                           })
                       .ToList();


            List<string> prompts = new List<string>();

            
            var openaiKey = config["OPENAI_KEY"];
            var openaiEndpoint = config["OPENAI_ENDPOINT"];
            var vectorSize = ulong.Parse(config["VECTOR_SIZE"]);
            var client = new OpenAIClient(openaiKey, new OpenAIClientOptions());

            string collectionName = "site_docs2";

            QdrantClient qdClient = new("localhost");
            var collections = await qdClient.ListCollectionsAsync();
            var q = (from collection in collections
                     where collection == collectionName
                     select collection).FirstOrDefault();
            if (q == null)
            {
                return new OkObjectResult("error ,  there is no collection");
                //VectorParams vp = new()
                //{
                //    Distance = Distance.Cosine,
                //    Size = vectorSize
                //};

                //await qdClient.CreateCollectionAsync(collectionName, vp);
            }

            List<PointStruct> points = new List<PointStruct>();
            foreach (var entity in groupedEntities)
            {
                prompts = new List<string>() { entity.Title + "  " + entity.EmbeddingText};
                EmbeddingsOptions eo = new(deploymentName: "text-embedding-ada-002",
                                        input: prompts);
                Response<Embeddings> response = await client.GetEmbeddingsAsync(eo);
               
                var random = new Random();
                foreach (var item in response.Value.Data)
                {
                    var embedding = item.Embedding;
                    int itemIndex = item.Index;

                    List<float> _vectors = new List<float>();

                    for (int i = 0; i < embedding.Length; i++)
                    {
                        float value = embedding.Span[i];
                        _vectors.Add(value);
                    }
                    PointStruct ps = new()
                    {
                        Id = GuidToULong(Guid.NewGuid()),
                        Payload =
                        {
                            ["text"] =  entity.Text,
                            ["title"] = entity.Title
                        },
                        Vectors = _vectors.ToArray()
                    };

                    points.Add(ps);

                }

            }

            await qdClient.UpsertAsync(collectionName, points);
            return new OkObjectResult("ok");
        }

        static ulong GuidToULong(Guid guid)
        {
            // Get the first 8 bytes of the Guid as a byte array
            byte[] bytes = guid.ToByteArray();
            ulong ulongValue = BitConverter.ToUInt64(bytes, 0);
            return ulongValue;
        }
        static string ExtractHrefValue(string input)
        {
            // Match the href attribute value using a regular expression
            System.Text.RegularExpressions.Match match = Regex.Match(input, "href='([^']*)'");

            // Check if a match is found and return the captured group value
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }

            // Return an empty string if no match is found
            return string.Empty;
        }
    }
}
