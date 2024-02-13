using Ardalis.GuardClauses;
using EmbeddingEngine.Core;
using Microsoft.Extensions.Configuration;
using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Tlv.Search.Common;
using VectorDb.Core;

namespace EventsIngestor
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                #region read configuration

                var builder = new ConfigurationBuilder()
                            .SetBasePath(Directory.GetCurrentDirectory())
                            .AddJsonFile("appsettings.json", optional: false);
                IConfiguration config = builder.Build();

                string? connectionString = config.GetConnectionString("AZURE_SQL_CONNECTIONSTRING");
                Guard.Against.NullOrEmpty(connectionString);

                var configKeyName = "VECTOR_DB_HOST";
                string? vectorDbHost = config[configKeyName];
                Guard.Against.NullOrEmpty(vectorDbHost, configKeyName, $"Couldn't find {configKeyName} in configuration");

                configKeyName = "VECTOR_DB_KEY";
                string? providerKey = config[configKeyName];
                Guard.Against.NullOrEmpty(providerKey, configKeyName, $"Couldn't find {configKeyName} in configuration");

                IVectorDb? sqlVectorDb = VectorDb.Core.VectorDb.Create(VectorDbProviders.SQLServer,
                                                                    vectorDbHost,
                                                                    connectionString);
                Guard.Against.Null(sqlVectorDb, "Couldn't create SQL vector db store");

                IVectorDb? qdrantDb = VectorDb.Core.VectorDb.Create(VectorDbProviders.QDrant,
                                                                    vectorDbHost,
                                                                    providerKey);
                Guard.Against.Null(qdrantDb, "Couldn't create QDrant vector db store");

                configKeyName = "EMBEDIING_PROVIDER";
                string? embeddingsProviderName = config[configKeyName];
                Guard.Against.NullOrEmpty(embeddingsProviderName, configKeyName, $"Couldn't find {configKeyName} in configuration");

                EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), embeddingsProviderName);

                configKeyName = $"{embeddingsProvider.ToString().ToUpper()}_KEY";
                string? embeddingEngineKey = config[configKeyName];
                Guard.Against.NullOrEmpty(embeddingEngineKey, configKeyName, $"Couldn't find {configKeyName} in configuration");

                configKeyName = $"{embeddingsProviderName.ToUpper()}_ENDPOINT";
                string? endpoint = config[configKeyName];
                Guard.Against.NullOrEmpty(endpoint);

                configKeyName = "EMBEDDING_MODEL_NAME";
                string? modelName = config[configKeyName];
                Guard.Against.NullOrEmpty(modelName, configKeyName, $"Couldn't find {configKeyName} in configuration");

                IEmbeddingEngine? embeddingEngine =
                        EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                                    providerKey: embeddingEngineKey,
                                                                    endpoint: endpoint,
                                                                    modelName);
                Guard.Against.Null(embeddingEngine, embeddingEngineKey, $"Couldn't create embedding engine with key '{embeddingEngineKey}'");

                int docIndex = 0;
                string collectionName = $"doc_parts_events_{embeddingEngine?.ProviderName}_{embeddingEngine?.ModelName}";
                collectionName = collectionName.Replace('/', '_');

                #endregion

                using HttpClient client = new HttpClient();

                var urls = new string[] {
                     "https://digitelmobile.tel-aviv.gov.il/SharepointData/api/ListData/WITH_Events/mobileapp",
                    "https://digitelmobile.tel-aviv.gov.il/SharepointData/api/ListData/אירועים/mobileapp"
                };

                foreach (var url in urls)
                {
                    Console.WriteLine($"Processing {url}");
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    string responseBody = await response.Content.ReadAsStringAsync();
                    Guard.Against.NullOrEmpty(responseBody, "Empty response from SP endpoint");
                    var options = new JsonSerializerOptions
                    {
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true,
                    };
                    options.Converters.Add(new DateTimeConverterUsingDateTimeParse());
                    options.Converters.Add(new FloatConverter());

                    Event[]? events = JsonSerializer.Deserialize<Event[]>(responseBody, options);
                    Guard.Against.Null(events, "Couldn't deserialize events");

                    foreach (var _event in events)
                    {
                        try
                        {
                            Doc doc = _event.ToDoc();
                            float[]? embeddings = await embeddingEngine.GenerateEmbeddingsAsync(doc.Content, "passage", logger: null);
                            if (embeddings is null)
                                continue;

                            //await sqlVectorDb.Save(doc,
                            //                        docIndex, 
                            //                        0, // parent doc id
                            //                        embeddings,
                            //                        collectionName);
                            await qdrantDb.Save(doc,
                                                docIndex,
                                                0,
                                                embeddings,
                                                collectionName);
                            Console.WriteLine($"processed doc {docIndex}");
                            docIndex++;
                        }
                        catch(Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }

                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
