using EmbeddingEngine.Core;
using Microsoft.Data.SqlClient;
using System.Data;
using Tlv.Search.Common;
using VectorDb.Core;

namespace VectorDb.SQLServer
{
    public class SQLServerStore(string hostUri, string providerKey) : IVectorDb
    {
        public string m_ConnectionString { get; set; } = providerKey;

        public Task<List<SearchItem>> Search(string collectionName,
                                            ReadOnlyMemory<float> queryVector,
                                            ulong limit = 5)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> Save(Doc doc,
                        int docIndex,
                        int parentDocId,
                        float[] vector,
                        string collectionName)
        {
            try
            {
                using var conn = new SqlConnection(m_ConnectionString);
                conn.Open();

                //
                // Store the document
                // 
                var command = new SqlCommand("storeDocument", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                command.Parameters.Add("@lang", SqlDbType.NVarChar, -1).Value = doc.Lang;
                //To-Do: extend stored procedure to include embeddingsProvider
                //command.Parameters.Add("@embeddingsProvider", SqlDbType.NVarChar, -1).Value = embeddingProviderName;
                command.Parameters.Add("@text", SqlDbType.NVarChar, -1).Value = doc.Text;
                command.Parameters.Add("@description", SqlDbType.NVarChar, -1).Value = doc.Description;
                command.Parameters.Add("@title", SqlDbType.NVarChar, -1).Value = doc.Title;
                command.Parameters.Add("@url", SqlDbType.NVarChar, -1).Value = doc.Url.ToString();
                command.Parameters.Add("@imageUrl", SqlDbType.NVarChar, -1).Value = doc.ImageUrl;
                command.Parameters.Add("@address", SqlDbType.NVarChar, -1).Value = doc.Address;
                command.Parameters.Add("@geom_location", SqlDbType.VarChar, -1).Value = 
                    $"POINT({doc.Lat} {doc.Lon})";

                var returnParameter = command.Parameters.Add("@ReturnVal", SqlDbType.Int);
                returnParameter.Direction = ParameterDirection.ReturnValue;

                int rowsUpdated = command.ExecuteNonQuery();
                doc.Id = (int)returnParameter.Value;

                //
                // Store embeddings vector
                // 

                SqlBulkCopy objbulk = new SqlBulkCopy(conn);
                objbulk.DestinationTableName = "site_docs_vector";
                objbulk.ColumnMappings.Add("doc_id", "doc_id");
                objbulk.ColumnMappings.Add("vector_value_id", "vector_value_id");
                objbulk.ColumnMappings.Add("vector_value", "vector_value");
                objbulk.ColumnMappings.Add("model_id", "model_id");

                DataTable tbl = new();
                tbl.Columns.Add(new DataColumn("doc_id", typeof(int)));
                tbl.Columns.Add(new DataColumn("vector_value_id", typeof(int)));
                tbl.Columns.Add(new DataColumn("vector_value", typeof(float)));
                tbl.Columns.Add(new DataColumn("model_id", typeof(int)));

                for (int i = 0; i < vector.Length; i++)
                {
                    DataRow dr = tbl.NewRow();
                    dr["doc_id"] = docIndex;
                    dr["vector_value_id"] = i;
                    dr["vector_value"] = vector[i];
                    dr["model_id"] = 0;

                    tbl.Rows.Add(dr);
                }

                await objbulk.WriteToServerAsync(tbl);

                return true;
            }
            catch (Exception)
            {
                throw;
            }

        }
    }
}