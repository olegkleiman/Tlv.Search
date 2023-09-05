using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System.Data;
using Phoenix.models;
using System.Text.Json;
using System.Net.Http.Json;
using System.Collections.Generic;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Phoenix
{
    internal class Program
    {
        static void Main(string[] args)
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

                var command = new SqlCommand("select * from dbo.config where is_enabled = 1", conn);
                using SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string? url = reader["url"] as string;
                    tasks.Add(Task.Run( async() => {

                        Console.WriteLine(url);
                        using var client = new HttpClient();
                        client.Timeout = TimeSpan.FromSeconds(40);
                        var items = await client.GetFromJsonAsync<List<SPItem>>(url);

                        foreach (var item in items)
                        {
                            //Console.WriteLine(item.details);
                            int rowId = item.Save(connectionString);
                            if ( rowId > 0 )
                                item.Embed(rowId, 
                                            connectionString,
                                           providerName: "openai", 
                                           providerKey: openaiKey);
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
            catch { }
        }
    }

}