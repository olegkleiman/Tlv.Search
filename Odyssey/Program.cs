using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Odyssey.models;
using Odyssey.Models;
using System.Data;

namespace Odyssey
{
    internal class Program
    {
        private static void saveDoc(Doc doc)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);
            IConfiguration config = builder.Build();

            var connectionString = config.GetConnectionString("AZURE_SQL_CONNECTIONSTRING");

            try
            { 
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                var command = new SqlCommand("storeDocument", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                command.Parameters.Add("@doc", SqlDbType.NVarChar, -1).Value = doc.doc;
                command.Parameters.Add("@title", SqlDbType.NVarChar, -1).Value = doc.title;
                command.Parameters.Add("@url", SqlDbType.NVarChar, -1).Value = doc.url;
                command.Parameters.Add("@source", SqlDbType.VarChar, -1).Value = doc.source;

                var returnParameter = command.Parameters.Add("@ReturnVal", SqlDbType.Int);
                returnParameter.Direction = ParameterDirection.ReturnValue;

                int rowsUpdated = command.ExecuteNonQuery();

                int rowId = (int)returnParameter.Value;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static async Task Main(string[] args)
        {
            //
            // Process sitemaps
            //
            var builder = new ConfigurationBuilder()
                            .SetBasePath(Directory.GetCurrentDirectory())
                            .AddJsonFile("appsettings.json", optional: false);
            IConfiguration config = builder.Build();

            var connectionString = config.GetConnectionString("AZURE_SQL_CONNECTIONSTRING");
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                var command = new SqlCommand("select * from doc_sources where type = 'sitemap'", conn);
                SqlDataReader reader = command.ExecuteReader();

                List<Task> tasks = new List<Task>();

                while (reader.Read())
                {
                    var siteMapUrl = reader.GetString(1);
 
                    Console.WriteLine($"Start processing {siteMapUrl}");
                    SiteMap? siteMap = SiteMap.Parse(new Uri(siteMapUrl));
                    if (siteMap == null)
                    {
                        Console.WriteLine($"Couldn't parse {siteMapUrl}");
                        continue;
                    }

                    Scrapper scrapper = new(siteMap);
                    await scrapper.Init();
                    Task task = scrapper.Scrap(saveDoc);
                    tasks.Add(task);
                }

                Task.WaitAll([.. tasks]);

            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
            }

            //SiteMap? siteMap = null;
            //using( var httpClient = new HttpClient() )      
            //{
            //    var url = new Uri("https://www.tel-aviv.gov.il/sitemap0.xml");
            //    var request = new HttpRequestMessage(HttpMethod.Get, url);
            //    string xmlDoc = httpClient.SendAsync(request).Result.Content.ReadAsStringAsync().Result;

            //    siteMap = SiteMap.Parse(url);
            //    if (siteMap == null)
            //        return; 
                
            //    Scrapper scrapper = new (siteMap);
            //    await scrapper.Init();
            //    await scrapper.Scrap(saveDoc);
            //}
        }

    }
}
