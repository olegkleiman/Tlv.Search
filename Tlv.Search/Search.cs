using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Tlv.Search.models;

namespace Tlv.Search
{
    public class Search
    {
        private readonly ILogger<Search>? _logger;

        public Search(ILogger<Search> log)
        {
            _logger = log;
        }

        [FunctionName("Search")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "q" })]
        [OpenApiParameter(name: "q", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **prompt** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req)
        {
 
            string? prompt = req.Query["q"];
            _logger.LogInformation($"Running search with prompt '{prompt}'");

            string? providerKey = Environment.GetEnvironmentVariable("OPENAI_KEY");
            // Azure OpenAI package
            OpenAIClient client = new(providerKey);
            Response<Embeddings>? response =
                client.GetEmbeddings("text-embedding-ada-002",
                                     new EmbeddingsOptions(prompt)
                                     );
            var _embedding = response.Value.Data[0].Embedding;

            string? connStr = Environment.GetEnvironmentVariable("CuriousityDB");
            try
            {
                using (SqlConnection? conn = new(connStr))
                {
                    conn.Open();

                    SqlCommand? command = new("calculateDistance", conn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    StringBuilder? sb = new("[");

                    float[]? arr = _embedding.ToArray<float>();
                    for (int i = 0; i < arr.Length; i++)
                    {
                        var item = arr[i];
                        sb.Append(item);
                        if (i == arr.Length - 1)
                            break;
                        sb.Append(',');
                    }
                    sb.Append(']');

                    List<SearchItem>? searchItems = new();

                    string? embedding = sb.ToString();
                    command.Parameters.Add("@vectorJson", SqlDbType.NVarChar, -1).Value
                        = embedding;

                    using (SqlDataAdapter? da = new())
                    {
                        da.SelectCommand = command;
                        da.SelectCommand.CommandType = CommandType.StoredProcedure;

                        DataSet? ds = new();
                        da.Fill(ds, "result_name");

                        DataTable? dt = ds.Tables["result_name"];
                        foreach (DataRow row in dt.Rows)
                        {
                            searchItems.Add(new SearchItem()
                            {
                                id = (int)row[0], 
                                title = (string)row[1],
                                //doc = (string)row[2],
                                url = (string)row[2],
                                distance = (double)row[3]
                            });
                        }
                    }

                    return new JsonResult(searchItems)
                    {
                        StatusCode = (int)HttpStatusCode.OK,
                        ContentType = "application/json"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }

        }
    }
}

