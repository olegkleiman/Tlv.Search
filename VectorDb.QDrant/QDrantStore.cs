﻿using Qdrant.Client;
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
        public string? m_providerKey { get; set; }// This is a host name (like 'localhost') for this provider
        QdrantClient m_qdClient;

        public QDrantStore(string providerKey)
        {
            m_providerKey = providerKey;
            m_qdClient = new QdrantClient(providerKey);
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

            await SearchGroups(collectionName,
                        "parent_doc_id",
                        queryVector,
                        limit: 200,
                        groupSize: 20);


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