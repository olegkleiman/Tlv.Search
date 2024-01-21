using Ardalis.GuardClauses;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Odyssey.Models;
using System.Data;
using EmbeddingEngine.Core;
using VectorDb.Core;
using Humanizer.Configuration;
using StackExchange.Redis;
using Scrapper;

namespace Odyssey
{
    internal class Program
    {
        static async Task Main(string[] args)
        {

            try
            {
                //
                // Process sitemaps
                //
                var builder = new ConfigurationBuilder()
                                .SetBasePath(Directory.GetCurrentDirectory())
                                .AddJsonFile("appsettings.json", optional: false);
                IConfiguration config = builder.Build();

                string configKeyName = "EMBEDIING_PROVIDER";
                string? embeddingsProviderName = config[configKeyName];
                Guard.Against.NullOrEmpty(embeddingsProviderName, configKeyName, $"Couldn't find {configKeyName} in configuration");

                EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), embeddingsProviderName);

                string? connectionString = config.GetConnectionString("AZURE_SQL_CONNECTIONSTRING");
                Guard.Against.NullOrEmpty(connectionString);

                configKeyName = $"{embeddingsProvider.ToString().ToUpper()}_KEY";
                string? embeddingEngineKey = config[configKeyName];
                Guard.Against.NullOrEmpty(embeddingEngineKey, configKeyName, $"Couldn't find {configKeyName} in configuration");

                configKeyName = "VECTOR_DB_KEY";
                string? providerKey = config[configKeyName];
                Guard.Against.NullOrEmpty(providerKey, configKeyName, $"Couldn't find {configKeyName} in configuration");

                configKeyName = "VECTOR_DB_HOST";
                string? vectorDbHost = config[configKeyName];
                Guard.Against.NullOrEmpty(vectorDbHost, configKeyName, $"Couldn't find {configKeyName} in configuration");

                configKeyName = "EMBEDDING_MODEL_NAME";
                string? modelName = config[configKeyName];
                Guard.Against.NullOrEmpty(modelName, configKeyName, $"Couldn't find {configKeyName} in configuration");

                using var conn = new SqlConnection(connectionString);
                string query = "select url,scrapper_id  from doc_sources where [type] = 'sitemap' and [isEnabled] = 1";

                conn.Open();

                using var da = new SqlDataAdapter(query, connectionString);
                var table = new DataTable();
                da.Fill(table);

                IVectorDb? vectorDb = VectorDb.Core.VectorDb.Create(VectorDbProviders.QDrant, vectorDbHost, providerKey);
                Guard.Against.Null(vectorDb, providerKey, $"Couldn't create vector db store with key '{providerKey}'");

                IEmbeddingEngine? embeddingEngine =
                    EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                                providerKey: embeddingEngineKey,
                                                                modelName);
                Guard.Against.Null(embeddingEngine, embeddingEngineKey, $"Couldn't create embedding engine with key '{embeddingEngineKey}'");

                Cache cache = new (config, "Redis");
                cache.ClearAll();

                List<Task<Dictionary<string, int>?>> tasks = [];
                foreach (DataRow? row in table.Rows)
                {
                    Guard.Against.Null(row);

                    object? val = row["url"];
                    if (val == null || val == DBNull.Value)
                        continue;

                    string? siteMapUrl = val.ToString();
                    if (string.IsNullOrEmpty(siteMapUrl))
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
                    Task<Dictionary<string, int>?> task = scrapper.ScrapTo(vectorDb, embeddingEngine!);
                    //Task task = scrapper.ScrapTo(memory);
                    tasks.Add(task);
                }

                await Task.WhenAll([.. tasks]);
                tasks.ForEach( task => 
                {
                    var dict = task.Result;
                    if (dict is null)
                        return;

                    cache.Merge(dict);

                });

            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
            }
        }

    }
}