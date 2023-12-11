using Azure;
using Azure.AI.OpenAI;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Odyssey.models;
using Odyssey.Models;
using System.Data;

namespace Odyssey
{
    internal class Program
    {
        private static void SaveAndEmbed(Doc doc)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);
            IConfiguration config = builder.Build();

            var connectionString = config.GetConnectionString("AZURE_SQL_CONNECTIONSTRING");

            var openaiKey = config["OPENAI_KEY"];

            if (doc == null)
                return;

            int rowId = saveDoc(doc, connectionString);
            if (rowId > 0)
            {
                doc.Id = rowId;
                Embed(doc, connectionString, openaiKey);
            }
        }

        private static void Embed(Doc doc, 
                                  string connectionString,
                                  string providerKey)
        {
            try
            {
                // Azure OpenAI package
                var client = new OpenAIClient(providerKey);
                string? _content = doc.doc;
                if (string.IsNullOrEmpty(_content))
                    return;

                using var conn = new SqlConnection(connectionString);
                conn.Open();

                SqlBulkCopy objbulk = new SqlBulkCopy(conn);
                objbulk.DestinationTableName = "site_docs_vector";
                objbulk.ColumnMappings.Add("doc_id", "doc_id");
                objbulk.ColumnMappings.Add("vector_value_id", "vector_value_id");
                objbulk.ColumnMappings.Add("vector_value", "vector_value");
                objbulk.ColumnMappings.Add("model_id", "model_id");

                Response<Embeddings> response =
                        client.GetEmbeddings("text-embedding-ada-002", 
                             new EmbeddingsOptions(_content)
                         );

                DataTable tbl = new();
                tbl.Columns.Add(new DataColumn("doc_id", typeof(int)));
                tbl.Columns.Add(new DataColumn("vector_value_id", typeof(int)));
                tbl.Columns.Add(new DataColumn("vector_value", typeof(float)));
                tbl.Columns.Add(new DataColumn("model_id", typeof(int)));

                foreach (var item in response.Value.Data)
                {
                    var embedding = item.Embedding;
                    for (int i = 0; i < embedding.Count; i++)
                    {
                        float value = embedding[i];

                        DataRow dr = tbl.NewRow();
                        dr["doc_id"] = doc.Id;
                        dr["vector_value_id"] = i;
                        dr["vector_value"] = value;
                        dr["model_id"] = 1;

                        tbl.Rows.Add(dr);
                    }
                }

                objbulk.WriteToServer(tbl);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

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

                command.Parameters.Add("@lang", SqlDbType.NVarChar, -1).Value = doc.lang;
                command.Parameters.Add("@doc", SqlDbType.NVarChar, -1).Value = doc.doc;
                command.Parameters.Add("@title", SqlDbType.NVarChar, -1).Value = doc.title;
                command.Parameters.Add("@url", SqlDbType.NVarChar, -1).Value = doc.url;
                command.Parameters.Add("@source", SqlDbType.VarChar, -1).Value = doc.source;

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

                    int scrapperId = reader.GetInt32(3);
                    Scrapper? scrapper = await Scrapper.Load(scrapperId, siteMap, connectionString);

                    //Scrapper scrapper = new(siteMap);
                    //await scrapper.Init();
                    Task task = scrapper.Scrap(SaveAndEmbed);
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
