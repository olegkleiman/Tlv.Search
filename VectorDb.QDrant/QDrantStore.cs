using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Collections;
using Tlv.Search.Common;
using VectorDb.Core;

namespace VectorDb.QDrant
{
    public class QDrantStore(string providerKey) : IVectorDb
    {
        public string? m_providerKey { get; set; } = providerKey; 

        const ulong VECTOR_SIZE = 1536;

        public List<Doc> Search(string prompt)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> Save(Doc doc, int docIndex, int parentDocId,
                                    float[] vector, string collectionName)
        {
            if (string.IsNullOrEmpty(collectionName))
                return false;

            try
            {
                QdrantClient qdClient = new(m_providerKey); // This is a host name (like 'localhost') for this provider
                var collections = await qdClient.ListCollectionsAsync();
                var q = (from collection in collections
                         where collection == collectionName
                         select collection).FirstOrDefault();
                if (q == null)
                {
                    VectorParams vp = new()
                    {
                        Distance = Distance.Cosine,
                        Size = (ulong)vector.Length
                    };

                    await qdClient.CreateCollectionAsync(collectionName, vp);
                }

                PointStruct ps = new()
                {
                    Id = (ulong)docIndex,
                    Payload =
                    {
                        ["text"] = doc.Text ?? string.Empty,
                        ["description"] = doc.Description ?? string.Empty,
                        ["title"] = doc.Title ?? string.Empty,
                        ["url"] = doc.Url ?? string.Empty,
                        ["image_url"] = doc.ImageUrl ?? string.Empty,
                        ["parent_doc_id"] = parentDocId
                    },
                    Vectors = vector
                };

                await qdClient.UpsertAsync(collectionName, [ps]);

                return true;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
                
            
        }

    }
}
