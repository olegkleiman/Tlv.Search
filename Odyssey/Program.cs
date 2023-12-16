using Ardalis.GuardClauses;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Odyssey.Models;
using System.Data;
using VectorDb.Core;

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

                string? connectionString = config.GetConnectionString("AZURE_SQL_CONNECTIONSTRING");
                Guard.Against.NullOrEmpty(connectionString);

                var openaiKey = config["OPENAI_KEY"];
                string? providerKey = config["PROVIDER_KEY"];

                using var conn = new SqlConnection(connectionString);
                string query = "select url,scrapper_id  from doc_sources where [type] = 'sitemap' and [isEnabled] = 1";
                
                conn.Open();

                using var da = new SqlDataAdapter(query, connectionString);
                var table = new DataTable();
                da.Fill(table);

                IVectorDb? vectorDb = VectorDb.Core.VectorDb.Create(VectorDbProviders.QDrant, providerKey);
                if (vectorDb is null)
                {
                    Console.WriteLine($"Couldn't create vector db store: {providerKey}");
                    return;
                }

                List<Task> tasks = [];
                foreach(DataRow? row in table.Rows)
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
                    Scrapper? scrapper = await Scrapper.Load(scrapperId, siteMap, connectionString);
                    if (scrapper == null)
                        continue;

                    Task task = scrapper.ScrapTo(vectorDb, openaiKey);
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
