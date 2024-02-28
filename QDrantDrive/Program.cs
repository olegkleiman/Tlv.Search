using Azure;
using Azure.AI.OpenAI;
using Tlv.Search.Common;
using Microsoft.Extensions.Configuration;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace QDrantDrive
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                //string prompt = "פעילות בקאנטרי גורן";
                string prompt = "פעילות יום אהבה";
                List<string> prompts = [prompt];

                #region read configuration
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false);
                IConfiguration config = builder.Build();

                var openaiKey = config["OPENAI_KEY"];
                var openaiEndpoint = config["OPENAI_ENDPOINT"];
                var vectorSize = ulong.Parse(config["VECTOR_SIZE"]);
                var client = new OpenAIClient(openaiKey, new OpenAIClientOptions());

                string context = "At which geographical location happens the following sentence. Report only the geographical name. Say 'No' if there is no geographical location detected. Answer in Hebrew: ";
                context += prompt;

                ChatCompletionsOptions cco = new ChatCompletionsOptions()
                {
                    Temperature = (float)0.7,
                    MaxTokens = 800,
                    NucleusSamplingFactor = (float)0.95,
                    FrequencyPenalty = 0,
                    PresencePenalty = 0,
                    DeploymentName = "gpt-4",
                    Messages =
                        {
                            new ChatRequestSystemMessage(@"You are a help assistant that analyzes the user input."),
                            new ChatRequestUserMessage(context)
                        },
                };
                var chat = await client.GetChatCompletionsAsync(cco);
                ChatResponseMessage responseMessage = chat.Value.Choices[0].Message;
                string content = responseMessage.Content;

                PromptContext PromptContext = new PromptContext(prompt);
                if (content.CompareTo("No") == 0
                    || string.IsNullOrEmpty(content))
                    PromptContext.GeoCondition = string.Empty;
                else
                    PromptContext.GeoCondition = content;

                string hostUri = config["VECTOR_DB_HOST"];
                string m_providerKey = config["VECTOR_DB_KEY"];
                string collectionName = config["COLLECTION_NAME"];

                #endregion

                QdrantClient qdClient = new QdrantClient(new Uri(hostUri),
                                                        apiKey: m_providerKey);

                var collections = await qdClient.ListCollectionsAsync();
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


                EmbeddingsOptions eo = new(deploymentName: "text-embedding-3-large",
                                            input: prompts);
                Response<Embeddings> response = await client.GetEmbeddingsAsync(eo);
                var queryVector = response.Value.Data[0].Embedding;


                //
                // Search: Full Text Match
                //
                // If there is no full-text index configured for the field, the condition will work as exact substring match.
                // var uRes = await qdClient.CreatePayloadIndexAsync(collectionName,
                //                                                    "description",
                //                                                    PayloadSchemaType.Text);


                Condition condition = new Condition()
                {
                    Field = new FieldCondition()
                    {
                        Key = "description",
                        Match = new Match()
                        {
                            // If the query has several words, then the condition will be satisfied only if all of them are present in the text.
                            Text = content
                        }
                    }
                };

                Filter filter = new Filter(condition);
                filter.Must.Add(condition);

                SearchParams sp = new SearchParams()
                {
                    Exact = true
                };

                List<SearchPoints> searches = [];
                SearchPoints searchPoints = new SearchPoints()
                {
                    CollectionName = collectionName,
                    WithPayload = true,
                    Filter = filter,
                    Limit = 5
                };
                searchPoints.Vector.Add(queryVector.ToArray());
                searches.Add(searchPoints);

                //var results = await qdClient.SearchBatchAsync(collectionName,
                //                                              searches);

                var _res = await qdClient.ScrollAsync(collectionName,
                                                      filter);

                var scores = await qdClient.SearchAsync(collectionName,
                                                        vector: queryVector.ToArray(),
                                                        filter: filter,
                                                        searchParams: sp,
                                                        limit: 5);
                foreach (var score in scores)
                {
                    var payload = score.Payload;
                    Console.WriteLine($"Text: {payload["text"]} Score: {score.Score}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
