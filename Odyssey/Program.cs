using Ardalis.GuardClauses;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Odyssey.Models;
using System.Data;
using EmbeddingEngine.Core;
using VectorDb.Core;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;

namespace Odyssey
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            const EmbeddingsProviders embeddingsProvider = EmbeddingsProviders.Gemini;
            try
            {
                //
                // Process sitemaps
                //
                var builder = new ConfigurationBuilder()
                                .SetBasePath(Directory.GetCurrentDirectory())
                                .AddJsonFile("appsettings.json", optional: false);
                IConfiguration config = builder.Build();

                string? connectionString = config.GetConnectionString("AZURE_SQL_CONNECTIONSTRING");
                Guard.Against.NullOrEmpty(connectionString);

                string keyName = $"{embeddingsProvider.ToString().ToLower()}_KEY";
                string? embeddingEngineKey = config[keyName];
                Guard.Against.NullOrEmpty(embeddingEngineKey, keyName, $"Couldn't find {keyName} in configuration");

                keyName = "VECTOR_DB_PROVIDER_KEY";
                string? providerKey = config[keyName];
                Guard.Against.NullOrEmpty(providerKey, keyName, $"Couldn't find {keyName} in configuration");

                keyName = "EMBEDDING_MODEL_NAME";
                string? modelName = config[keyName];
                Guard.Against.NullOrEmpty(modelName, keyName, $"Couldn't find {modelName} in configuration");

                using var conn = new SqlConnection(connectionString);
                string query = "select url,scrapper_id  from doc_sources where [type] = 'sitemap' and [isEnabled] = 1";
                
                conn.Open();

                using var da = new SqlDataAdapter(query, connectionString);
                var table = new DataTable();
                da.Fill(table);

                IVectorDb? vectorDb = VectorDb.Core.VectorDb.Create(VectorDbProviders.QDrant, providerKey);
                if (vectorDb is null)
                {
                    Console.WriteLine($"Couldn't create vector db store with key '{providerKey}'");
                    return;
                }

                IEmbeddingEngine? embeddingEngine = 
                    EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider, 
                                                                providerKey: embeddingEngineKey,
                                                                modelName: modelName);
                if( embeddingEngine is null )
                {
                    Console.WriteLine($"Couldn't create embedding engine with key '{embeddingEngineKey}'");
                    return;
                }

                var openaiAzureKey = config["OPENAI_AZURE_KEY"];
                if (string.IsNullOrEmpty(openaiAzureKey))
                {
                    Console.WriteLine("Azure OpenAI key not found in configuration");
                    return;
                }
                var openaiEndpoint = config["OPENAI_ENDPOINT"];
                if (string.IsNullOrEmpty(openaiEndpoint))
                {
                    Console.WriteLine("OpenAI endpoint not found in configuration");
                    return;
                }

#pragma warning disable SKEXP0003, SKEXP0011, SKEXP0026

                //var qdClient = new QdrantVectorDbClient("http://localhost:6333", 1536);
                //IMemoryStore memoryStore = new QdrantMemoryStore(qdClient);
                //bool b = await memoryStore.DoesCollectionExistAsync("site_docs");
                //memoryStore.DeleteCollectionAsync("site_docs");
                var memoryBuilder = new MemoryBuilder()
                                .WithAzureOpenAITextEmbeddingGeneration("ada2",
                                                                    openaiEndpoint,
                                                                    openaiAzureKey)
                                .WithQdrantMemoryStore("http://localhost:6333", 1536);
                                //.WithMemoryStore(memoryStore);

                ISemanticTextMemory memory = memoryBuilder.Build();

#pragma warning restore SKEXP0003, SKEXP0011, SKEXP0026

                List<Task> tasks = [];
                foreach (DataRow? row in table.Rows)
                {
                    Guard.Against.Null(row);

                    object? val = row["url"];
                    if( val == null || val == DBNull.Value)
                        continue;

                    string? siteMapUrl = val.ToString();
                    if( string.IsNullOrEmpty(siteMapUrl) )
                        continue;

                    Console.WriteLine($"Start processing {siteMapUrl}");
                    Uri uri = new(siteMapUrl); 
                    SiteMap? siteMap = SiteMap.Parse(uri);
                    if (siteMap == null)
                    {
                        Console.WriteLine($"Couldn't parse {siteMapUrl}");
                        continue;
                    }

                    int scrapperId = (int)row["scrapper_id"];

                    Scrapper? scrapper = Scrapper.Load(scrapperId, siteMap, connectionString);
                    if (scrapper == null)
                        continue;

                    await scrapper.Init();
                    Task task = scrapper.ScrapTo(vectorDb, embeddingsProvider, embeddingEngine);
                    //Task task = scrapper.ScrapTo(memory);
                    tasks.Add(task);
                }

                Task.WaitAll([.. tasks]);

            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
            }
        }

    }
}
