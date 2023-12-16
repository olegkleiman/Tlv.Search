using Azure;
using Azure.AI.OpenAI;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Collections;
using System.Net.Sockets;
using VectorDb.Core;

namespace VectorDb.QDrant
{
    public class QDrantStore(string providerKey) : IVectorDb
    {
        public string? m_providerKey { get; set; } = providerKey; // 

        const ulong VECTOR_SIZE = 1536;

        public async Task<bool> Embed(Doc doc, ulong docIndex, string key)
        {
            try
            {
                QdrantClient qdClient = new(m_providerKey); // This is a host name (like 'localhost') for this provider

                string collectionName = "site_docs";
                var collections = await qdClient.ListCollectionsAsync();
                var q = (from collection in collections
                         where collection == collectionName
                         select collection).FirstOrDefault();
                if (q == null)
                {
                    VectorParams vp = new()
                    {
                        Distance = Distance.Cosine,
                        Size = VECTOR_SIZE
                    };

                    await qdClient.CreateCollectionAsync(collectionName, vp);
                }

                string? _content = doc.Content;
                Response<Embeddings> response = await EmbedInternal(key, _content);

                List<PointStruct> points = [];
                //int index = 0;
                foreach (var item in response.Value.Data)
                {
                    var embedding = item.Embedding;
                    //int itemIndex = item.Index;

                    List<float> _vectors = [];
                    for (int i = 0; i < embedding.Length; i++)
                    {
                        float value = embedding.Span[i];
                        _vectors.Add(value);
                    }
                    PointStruct ps = new()
                    {
                        Id = docIndex,
                        Payload =
                        {
                            ["text"] = doc.Description,
                            ["title"] = doc.Title,
                            ["url"] = doc.Url
                        },
                        Vectors = _vectors.ToArray()
                    };
                    points.Add(ps);
                    Console.WriteLine($"String {doc.Url}");
                }

                await qdClient.UpsertAsync(collectionName, points);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

            return true;
        }

        private async Task<Response<Embeddings>> EmbedInternal(string key, string content)
        {
            try
            {
                var client = new OpenAIClient(key, new OpenAIClientOptions());
                EmbeddingsOptions eo = new()
                {
                    DeploymentName = "text-embedding-ada-002",
                    Input = [content]
                };
                return await client.GetEmbeddingsAsync(eo);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<bool> Save(Doc doc)
        {
            return true;
        }
    }
}
