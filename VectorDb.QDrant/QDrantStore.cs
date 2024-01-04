using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Collections.Generic;
using Tlv.Search.Common;
using VectorDb.Core;

namespace VectorDb.QDrant
{
    public class QDrantStore : IVectorDb
    {
        public string? m_providerKey { get; set; }// This is a host name (like 'localhost') for this provider
        QdrantClient m_qdClient;

        public QDrantStore(string providerKey)
        {
            m_providerKey = providerKey;
            m_qdClient = new QdrantClient(providerKey);
        }
        public async Task<List<SearchItem>> Search(string collectionName,
                                                 ReadOnlyMemory<float> queryVector,
                                                 ulong limit = 5)
        {
            Filter filter = new Filter()
            {

            };
            SearchParams sp = new SearchParams()
            {
                Exact = true
            };

            IReadOnlyList<ScoredPoint> scores = await m_qdClient.SearchAsync(collectionName,
                                                    queryVector,
                                                    filter: filter,
                                                    searchParams: sp,
                                                    limit: limit);


            return (from score in scores
                    let payload = score.Payload
                    select new SearchItem()
                    {
                        id = score.Id.Num,
                        title = payload["title"].StringValue,
                        summary = payload["text"].StringValue,
                        url = payload["url"].StringValue,
                        imageUrl = payload["image_url"].StringValue,
                        similarity = score.Score
                    }).ToList();

        }

        public async Task<bool> Save(Doc doc,
                        int docIndex,
                        int parentDocId,
                        float[] vector,
                        string collectionName)
        {
            if (string.IsNullOrEmpty(collectionName))
                return false;

            try
            {
                var collections = await m_qdClient.ListCollectionsAsync();
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

                    await m_qdClient.CreateCollectionAsync(collectionName, vp);
                }

                PointStruct ps = new()
                {
                    Id = (ulong)docIndex,
                    Payload =
                    {
                        ["text"] = doc.Text ?? string.Empty,
                        //["summary"] = doc.Summary ?? string.Empty,
                        //["embeddingProvider"] = embeddingProviderName ?? string.Empty,
                        ["description"] = doc.Description ?? string.Empty,
                        ["title"] = doc.Title ?? string.Empty,
                        ["url"] = doc.Url ?? string.Empty,
                        ["image_url"] = doc.ImageUrl ?? string.Empty,
                        ["parent_doc_id"] = parentDocId
                    },
                    Vectors = vector
                };

                await m_qdClient.UpsertAsync(collectionName, new List<PointStruct>() { ps } );

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }


        }

    }
}