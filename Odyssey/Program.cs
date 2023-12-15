using Ardalis.GuardClauses;
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
            if (doc == null)
                return;

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);
            IConfiguration config = builder.Build();

            var connectionString = config.GetConnectionString("AZURE_SQL_CONNECTIONSTRING");
            if (string.IsNullOrEmpty(connectionString))
                throw new ApplicationException("No connection string when saving scrapped document");

            var openaiKey = config["OPENAI_KEY"];
            if( string.IsNullOrEmpty(openaiKey) )
                throw new ApplicationException("No OPENAI key for embedding scrapped document");

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
                string? _content = doc.Content;
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

                //List<string> inputs = new List<string>();
                //inputs.Add(_content);

                EmbeddingsOptions eo = new EmbeddingsOptions()
                {
                    DeploymentName = "text-embedding-ada-002",
                    Input = [_content]
                };

                Response<Embeddings> response = client.GetEmbeddings(eo);

                DataTable tbl = new();
                tbl.Columns.Add(new DataColumn("doc_id", typeof(int)));
                tbl.Columns.Add(new DataColumn("vector_value_id", typeof(int)));
                tbl.Columns.Add(new DataColumn("vector_value", typeof(float)));
                tbl.Columns.Add(new DataColumn("model_id", typeof(int)));

                foreach (var item in response.Value.Data)
                {
                    var embedding = item.Embedding;
                    for (int i = 0; i < embedding.Length; i++)
                    {
                        float value = embedding.Span[i];

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

                command.Parameters.Add("@lang", SqlDbType.NVarChar, -1).Value = doc.Lang;
                command.Parameters.Add("@text", SqlDbType.NVarChar, -1).Value = doc.Text;
                command.Parameters.Add("@description", SqlDbType.NVarChar, -1).Value = doc.Description;
                command.Parameters.Add("@title", SqlDbType.NVarChar, -1).Value = doc.Title;
                command.Parameters.Add("@url", SqlDbType.NVarChar, -1).Value = doc.Url;
                command.Parameters.Add("@imageUrl", SqlDbType.VarChar, -1).Value = doc.ImageUrl;
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

                    Task task = scrapper.Scrap(SaveAndEmbed);
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
