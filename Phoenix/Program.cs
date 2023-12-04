using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System.Data;
using Phoenix.models;
using System.Text.Json;
using System.Net.Http.Json;
using System.Collections.Generic;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.CommandLine;
using System.ComponentModel;
using System.Net;
using System;
using HtmlAgilityPack;
using System.Text;
using System.Web;
using Microsoft.VisualBasic;

namespace Phoenix
{
    public class DateTimeConverterUsingDateTimeParse : System.Text.Json.Serialization.JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, 
            Type typeToConvert, 
            JsonSerializerOptions options)
        {
            string? str = reader.GetString();
            DateTime dt;
            return DateTime.TryParse(str, out dt) ? dt : DateTime.MinValue;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    public class FloatConverterUsingDateTimeParse : System.Text.Json.Serialization.JsonConverter<float>
    {
        public override float Read(ref Utf8JsonReader reader, 
            Type typeToConvert, 
            JsonSerializerOptions options)
        {
            string? str = reader.GetString();
            float value;
            return float.TryParse(str, out value) ? value : float.MinValue;
        }

        public override void Write(Utf8JsonWriter writer, 
                                    float value, 
                                    JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    internal class Program
    {
        internal static async Task<int> MainProcess(string provider, 
                                                    string modelName)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);
            IConfiguration config = builder.Build();

            var connectionString = config.GetConnectionString("AZURE_SQL_CONNECTIONSTRING");
            var tasks = new List<Task>();

            var openaiKey = config["OPENAI_KEY"];

            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                // SqlTransaction transaction = conn.BeginTransaction();

                int modelId = 0;
                SqlCommand command = new($"select * from embedding_providers where [provider_name] = '{provider}' and [model_name] = '{modelName}'", 
                                        conn);
                //command.Transaction = transaction;
                using SqlDataReader _reader = command.ExecuteReader();
                if( _reader.Read() )
                {
                    modelId = (int)_reader.GetInt32(0);
                }
                _reader.Close();

                //
                // Clear all docs corpus
                //
                command = new SqlCommand("clearAll", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                //command.Transaction = transaction;
                command.ExecuteNonQuery();

                //
                // Process sitemaps
                //
                //SiteMap? siteMap = null;
                //using (var httpClient = new HttpClient())
                //{
                //    var request = new HttpRequestMessage(HttpMethod.Get, "https://www.tel-aviv.gov.il/sitemap0.xml");
                //    string xmlDoc = httpClient.SendAsync(request).Result.Content.ReadAsStringAsync().Result;

                //    siteMap = SiteMap.Parse(xmlDoc);
                //    if (siteMap == null)
                //        return 1;

                //    foreach(var item in siteMap.items)
                //    {
                //        await item.DownloadAndSave(httpClient, connectionString);
                //    }

                //}

                command = new SqlCommand("select * from dbo.config where is_enabled = 1", conn);
                using SqlDataReader reader = command.ExecuteReader();

                while( reader.Read() )
                {
                    string? url = reader["url"] as string;
                    tasks.Add(Task.Run(async () => {

                        try
                        {
                            Console.WriteLine(url);
                            using var client = new HttpClient();
                            client.Timeout = TimeSpan.FromSeconds(40);

                            JsonSerializerOptions options = new JsonSerializerOptions();
                            options.Converters.Add(new DateTimeConverterUsingDateTimeParse());
                            options.Converters.Add(new FloatConverterUsingDateTimeParse());
                            var items = await client.GetFromJsonAsync<List<SPItem>>(url, options);
                            if (items == null)
                                return;

                            foreach (var item in items)
                            {
                                int rowId = item.Save(connectionString, 
                                                      source: "SharePoint");
                                if (rowId > 0)
                                    item.Embed(rowId,
                                               connectionString,
                                               modelName: modelName,
                                               modelId: modelId,
                                               providerKey: openaiKey); ;
                            }
                        }
                        catch(Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                        }

                    })
                    );
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                //tra
            }

            Task t = Task.WhenAll(tasks);
            try
            {
                t.Wait();
            }
            catch
            {
                return 1;
            }
            return 0;
        }

        static int Main(string[] args)
        {
            Option<string> providerOption = new(
                aliases: new[] { "--provider", "-p" },
                description: "Provider Name"
                );

            Option<string> modelNameOption = new(
                aliases: new[] { "--modelName", "-m" },
                description: "Model Name"
                );

            RootCommand rootCommand = new(description: "Embed SP items with helps of model")
            {
                providerOption,
                modelNameOption
            };
            rootCommand.SetHandler(
                (string provider, string modelName) =>
                {
                    MainProcess(provider, modelName);
                },
                providerOption,
                modelNameOption
                );
            //await rootCommand.InvokeAsync(args);
            rootCommand.Invoke(args);

            return 0;
        }
    }

}