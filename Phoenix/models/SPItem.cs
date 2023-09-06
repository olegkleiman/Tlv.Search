﻿using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using HtmlAgilityPack;
using Microsoft.Data.SqlClient;
using OpenAI_API;
using OpenAI_API.Embedding;
using OpenAI_API.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Phoenix.models
{
    internal class SPItem
    {
        public string? title { get; set; }

        [JsonPropertyName("fileRef")]
        public string? url { get; set; }
        public string? details { get; set; }
        public string? content { get; set; }
        public string? comments { get; set; }
        public string? remarks { get; set; }
        public string? summary { get; set; }

        private string? getContent()
        {
            string _content = String.Empty;

            if (!string.IsNullOrEmpty(this.details))
                _content = this.details;
            else if (!string.IsNullOrEmpty(this.content))
                _content = this.content;
            else if (!string.IsNullOrEmpty(this.comments))
                _content = this.comments;
            else if (!string.IsNullOrEmpty(this.remarks))
                _content = this.remarks;
            else if( !string.IsNullOrEmpty(this.summary))
                _content = this.summary;

            return _content;
        }

        public void Embed(int docId, 
                          string connectionString,
                          string providerName,
                          string providerKey)
        {
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                var command = new SqlCommand($@"select id from [dbo].[embedding_providers] where [provider_name] = '{providerName}'", 
                                conn);
                using SqlDataReader reader = command.ExecuteReader();
                int providerId = 0;
                if (reader.Read())
                {
                    providerId = (int)reader["id"];
                }
                else
                    return;
                reader.Close();

                SqlBulkCopy objbulk = new SqlBulkCopy(conn);
                objbulk.DestinationTableName = "site_docs_vector";
                objbulk.ColumnMappings.Add("doc_id", "doc_id");
                objbulk.ColumnMappings.Add("vector_value_id", "vector_value_id");
                objbulk.ColumnMappings.Add("vector_value", "vector_value");
                objbulk.ColumnMappings.Add("embedding_provider_id", "embedding_provider_id");

                // Azure OpenAI package
                var client = new OpenAIClient(providerKey);
                string? _content = this.getContent();
                if (string.IsNullOrEmpty(_content))
                    return;

                Response<Embeddings> response = 
                    client.GetEmbeddings("text-embedding-ada-002", 
                                         new EmbeddingsOptions(_content)
                                         );
                DataTable tbl = new();
                tbl.Columns.Add(new DataColumn("doc_id", typeof(Int32)));
                tbl.Columns.Add(new DataColumn("vector_value_id", typeof(Int32)));
                tbl.Columns.Add(new DataColumn("vector_value", typeof(float)));
                tbl.Columns.Add(new DataColumn("embedding_provider_id", typeof(int)));
                
                foreach (var item in response.Value.Data)
                {
                    var embedding = item.Embedding;
                    for(int i = 0; i < embedding.Count; i++)
                    {
                        float value = embedding[i];
                        
                        DataRow dr = tbl.NewRow();
                        dr["doc_id"] = docId;
                        dr["vector_value_id"] = i;
                        dr["vector_value"] = value;
                        dr["embedding_provider_id"] = providerId;

                        tbl.Rows.Add(dr);
                    }
                }

                objbulk.WriteToServer(tbl);

                // OpenAI package
                // OpenAIAPI api = new OpenAIAPI("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");
                //var result = await api.Embeddings.CreateEmbeddingAsync(
                //        new EmbeddingRequest(Model.AdaTextEmbedding, _content)
                //    );
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        
        }

        public int Save(string connectionString)
        {
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                string? _content = getContent();
                if (string.IsNullOrEmpty(_content))
                    return 0;

                //
                // Clean the document
                //
                HtmlDocument htmlDoc = new();
                htmlDoc.LoadHtml(_content);
                string cleanText = htmlDoc.DocumentNode.InnerText;

                var command = new SqlCommand("storeDocument", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                command.Parameters.Add("@doc", SqlDbType.NVarChar, -1).Value = cleanText;
                command.Parameters.Add("@title", SqlDbType.NVarChar, -1).Value = title;
                command.Parameters.Add("@url", SqlDbType.NVarChar, -1).Value = url;

                var returnParameter = command.Parameters.Add("@ReturnVal", SqlDbType.Int);
                returnParameter.Direction = ParameterDirection.ReturnValue;

                int rowsUpdated = command.ExecuteNonQuery();

                int rowId = (int)returnParameter.Value;

                return rowId;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return 0;
            }
        }
    }
}
