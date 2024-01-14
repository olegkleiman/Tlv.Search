using Ardalis.GuardClauses;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Collections;
using System.Collections.Generic;
using Tlv.Search.Common;
using VectorDb.Core;
using static System.Formats.Asn1.AsnWriter;

namespace VectorDb.QDrant
{
    public class QDrantStore : IVectorDb
    {
        public string? m_hostUrl; // This is a host name (like 'localhost') for this provider
        public string? m_providerKey { get; set; }
        QdrantClient m_qdClient;

        public QDrantStore(string hostUrl,
                           string providerKey)
        {
            m_hostUrl = hostUrl;
            m_providerKey = providerKey;
            m_qdClient = new QdrantClient(m_hostUrl, apiKey: m_providerKey);
        }

        public async Task SearchGroups(string collectionName,
                                        string groupBy,
                                        ReadOnlyMemory<float> queryVector,
                                        uint limit = 5,
                                        uint groupSize = 20)
        {
            var scores = await m_qdClient.SearchGroupsAsync(collectionName,
                                         queryVector,
                                         groupBy,
                                         limit: limit,
                                         groupSize: groupSize);
            foreach (var score in scores)
            {
                await Console.Out.WriteLineAsync(score.Id.ToString());
                new SearchGroupByItem()
                {
                    id = score.Id.StringValue,
                };
            }
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
                Exact = true,
            };

            // Retrieves closest points based on vector similarity
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
                        parentDocId = payload["parent_doc_id"].IntegerValue,
                        similarity = score.Score
                    }).ToList();

        }

        public async Task<bool> Save(Doc doc,
                        int docIndex,
                        int parentDocId,
                        float[] vector,
                        string collectionName)
        {
            Guard.Against.NullOrEmpty(collectionName);
            Guard.Against.Null(vector);

            try
            {
                string _collectionName = collectionName.Replace('/', '_');

                var collections = await m_qdClient.ListCollectionsAsync();
                var q = (from collection in collections
                         where collection == _collectionName
                         select collection).FirstOrDefault();
                if (q == null)
                {
                    VectorParams vp = new()
                    {
                        Distance = Distance.Cosine,
                        Size = (ulong)vector.Length
                    };

                    await m_qdClient.CreateCollectionAsync(_collectionName, vp);
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
                        ["url"] = doc.Url.ToString() ?? string.Empty,
                        ["image_url"] = doc.ImageUrl ?? string.Empty,
                        ["parent_doc_id"] = parentDocId
                    },
                    Vectors = vector
                };

                await m_qdClient.UpsertAsync(_collectionName, new List<PointStruct>() { ps } );

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