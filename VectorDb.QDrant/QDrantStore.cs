using Ardalis.GuardClauses;
using Google.Protobuf.Collections;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Tlv.Search.Common;
using VectorDb.Core;

namespace VectorDb.QDrant
{
    public class Location
    {
        public float Lat { get; set; }
        public float Lon { get; set; }
    }

    public class QDrantStore : IVectorDb
    {
        //public Uri? m_hostUri; // This is a host name (like 'localhost') for this provider
        public string? m_providerKey { get; set; }
        QdrantClient m_qdClient;

        public QDrantStore(string hostUri,
                           string providerKey)
        {
            m_providerKey = providerKey;

            try
            {
                var _hostUri = new Uri(hostUri);
                m_qdClient = new QdrantClient(_hostUri, apiKey: m_providerKey);
            }
            catch (UriFormatException ex)
            {
                m_qdClient = new QdrantClient(hostUri, apiKey: m_providerKey);
            }

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
                                                 PromptContext promptContext,
                                                 ulong limit = 5)
        {
            List<SearchItem> searchResult = new ();

            if (!string.IsNullOrEmpty(promptContext.GeoCondition))
            {
                Condition condition = new()
                {
                    Field = new FieldCondition()
                    {
                        Key = "description",
                        Match = new Match()
                        {
                            // If the query has several words, then the condition will be satisfied only if all of them are present in the text.
                            Text = promptContext.GeoCondition
                        }
                    }
                };
                Filter filter = new(condition);

                ScrollResponse scrollResponse = await m_qdClient.ScrollAsync(collectionName, filter);
                RepeatedField<RetrievedPoint> scrollPoints = scrollResponse.Result;
                var _q = (from point in scrollPoints
                         let payload = point.Payload
                         select new SearchItem()
                         {
                             id = point.Id.Num,
                             title = payload["title"].StringValue,
                             summary = payload["text"].StringValue,
                             url = payload["url"].StringValue,
                             imageUrl = payload["image_url"].StringValue,
                             parentDocId = payload["parent_doc_id"].IntegerValue,
                         }
                        ).ToList();
                searchResult.AddRange(_q);
            }

            SearchParams sp = new()
            {
                Exact = true,
            };

            // Retrieves closest points based on vector similarity
            IReadOnlyList<ScoredPoint> scoredPoints = await m_qdClient.SearchAsync(collectionName,
                                                    queryVector,
                                                    filter: null,
                                                    searchParams: sp,
                                                    limit: limit);


            var q = (from point in scoredPoints
                    let payload = point.Payload
                    select new SearchItem()
                    {
                        id = point.Id.Num,
                        title = payload["title"].StringValue,
                        summary = payload["text"].StringValue,
                        url = payload["url"].StringValue,
                        imageUrl = payload["image_url"].StringValue,
                        parentDocId = payload["parent_doc_id"].IntegerValue,
                        similarity = point.Score
                    }).ToList();
            searchResult.AddRange(q);
            return searchResult;
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
                var collections = await m_qdClient.ListCollectionsAsync();
                var q = (from collection in collections
                         where collection == collectionName
                         select collection).FirstOrDefault();
                if (q == null)
                {
                    VectorParams vp = new()
                    {
                        Distance = Distance.Cosine,
                        Size = (ulong)vector.Length,

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
                        ["url"] = doc.Url.ToString() ?? string.Empty,
                        ["image_url"] = doc.ImageUrl ?? string.Empty,
                        ["parent_doc_id"] = parentDocId,
                        ["location"] = $"{{ \"lat\":{doc.Lat}, \"lon\":{doc.Lon} }}",
                        ["prompt"] = "none"
                    },
                    Vectors = vector
                };

                await m_qdClient.UpsertAsync(collectionName, new List<PointStruct>() { ps });

                return true;
            }
            catch (QdrantException)
            {
                throw;
            }

        }

    }
}