using Ardalis.GuardClauses;
using Azure;
//using Azure.AI.OpenAI;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Odyssey.Models;
using System.Data;
using VectorDb.Core;

namespace Odyssey
{
    internal class Program
    {

        private static int saveDoc(Doc doc, string connectionString)
        {
            try
            { 
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                var command = new SqlCommand("storeDocument", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                command.Parameters.Add("@lang", SqlDbType.NVarChar, -1).Value = doc.Lang;
                command.Parameters.Add("@text", SqlDbType.NVarChar, -1).Value = doc.Text;
                command.Parameters.Add("@description", SqlDbType.NVarChar, -1).Value = doc.Description;
                command.Parameters.Add("@title", SqlDbType.NVarChar, -1).Value = doc.Title;
                command.Parameters.Add("@url", SqlDbType.NVarChar, -1).Value = doc.Url;
                command.Parameters.Add("@imageUrl", SqlDbType.NVarChar, -1).Value = doc.ImageUrl;
                command.Parameters.Add("@source", SqlDbType.VarChar, -1).Value = doc.Source;

                var returnParameter = command.Parameters.Add("@ReturnVal", SqlDbType.Int);
                returnParameter.Direction = ParameterDirection.ReturnValue;

                int rowsUpdated = command.ExecuteNonQuery();

                int rowId = (int)returnParameter.Value;
                return rowId;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 0;
            }
        }

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
                string? providerKey = config["QDRANT_PROVIDER_KEY"];

                using var conn = new SqlConnection(connectionString);
                string query = "select url,scrapper_id  from doc_sources where [type] = 'sitemap' and [isEnabled] = 1";
                
                conn.Open();

                using var da = new SqlDataAdapter(query, connectionString);
                var table = new DataTable();
                da.Fill(table);

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

                    //Task task = scrapper.Scrap(SaveAndEmbed);
                    IVectorDb? vectorDb = VectorDb.Core.VectorDb.Create(VectorDbProviders.QDrant, providerKey);
                    if (vectorDb is not null)
                    {
                        Task task = scrapper.ScrapTo(vectorDb, openaiKey);
                        tasks.Add(task);
                    }
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
