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

namespace Phoenix
{
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

                int modelId = 0;
                SqlCommand command = new($"select * from embedding_providers where [provider_name] = '{provider}' and [model_name] = '{modelName}'", 
                                        conn);
                using SqlDataReader _reader = command.ExecuteReader();
                if( _reader.Read() )
                {
                    modelId = (int)_reader.GetInt32(0);
                }
                _reader.Close();

                command = new SqlCommand("select * from dbo.config where is_enabled = 1", conn);
                using SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string? url = reader["url"] as string;
                    tasks.Add(Task.Run(async () => {

                        Console.WriteLine(url);
                        using var client = new HttpClient();
                        client.Timeout = TimeSpan.FromSeconds(40);
                        var items = await client.GetFromJsonAsync<List<SPItem>>(url);
                        if (items == null)
                            return;

                        foreach (var item in items)
                        {
                            //Console.WriteLine(item.details);
                            int rowId = item.Save(connectionString);
                            if (rowId > 0)
                                item.Embed(rowId,
                                           connectionString,
                                           modelName: modelName,
                                           modelId: modelId,
                                           providerKey: openaiKey); ;
                        }

                    })
                    );
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
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