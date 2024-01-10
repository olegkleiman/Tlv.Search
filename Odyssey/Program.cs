using Ardalis.GuardClauses;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Odyssey.Models;
using System.Data;
using EmbeddingEngine.Core;
using VectorDb.Core;
using System.Xml.Linq;
using Tlv.Search.Common;
using Azure.AI.OpenAI;

namespace Odyssey
{
    internal class Program
    {
        static async Task Main(string[] args)
        {

            try
            {

                XDocument xDoc = XDocument.Load("arnona_ex.xml");

                // read all 'url' tags

                // foreach tag
                // 1. execute 'source' attribute
                // 1a. Get the response as JSON
                // 2. Call to handler url and pass a JSON as body


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

                configKeyName = "VECTOR_DB_HOST";
                string? vectorDbHost = config[configKeyName];
                Guard.Against.NullOrEmpty(vectorDbHost, configKeyName, $"Couldn't find {configKeyName} in configuration");

                configKeyName = "VECTOR_DB_KEY";
                string? providerKey = config[configKeyName];
                Guard.Against.NullOrEmpty(providerKey, configKeyName, $"Couldn't find {configKeyName} in configuration");

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
                                                                providerKey: embeddingEngineKey);
                Guard.Against.Null(embeddingEngine, embeddingEngineKey, $"Couldn't create embedding engine with key '{embeddingEngineKey}'");

                List<Task> tasks = [];
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

                    //
                    // Start test
                    List<Doc> docs =
                    [
                        new Doc()   
                        {
                            Id = 1000,
                            Title = "אזרח ותיק",
                            Text = @" - אזרחים ותיקים המקבלים קצבה מהמוסד לביטוח לאומי\\n\\n\  
     - אזרחים ותיקים המקבלים קצבה מהמוסד לביטוח לאומי ובנוסף מקבלים גמלת הבטחת הכנסה\\n\\n
- אזרחים ותיקים (הנחה על פי מבחן הכנסה)\\n\\n,
אזרחים ותיקים המקבלים קצבת זקנה לנכה - \n\n\
מקבלי גמלת סיעוד",
                        },

                    ];
                    foreach (var _doc in docs)
                    {

                        string input = _doc.Content ?? string.Empty;
                        float[]? embeddings = await embeddingEngine.GenerateEmbeddingsAsync(input);

                        await vectorDb.Save(_doc,
                                        _doc.Id,
                                        0, // parent doc id
                                        embeddings,
                                        $"doc_parts_OPENAI" // collection name
                               );
                    }
                    //
                    // End Test

                    await scrapper.Init();
                    Task task = scrapper.ScrapTo(vectorDb, embeddingEngine!);
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