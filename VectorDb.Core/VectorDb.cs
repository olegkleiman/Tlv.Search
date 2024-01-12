using Ardalis.GuardClauses;
using EmbeddingEngine.Core;
using System.Runtime.Remoting;
using Tlv.Search.Common;

namespace VectorDb.Core
{
    public interface IVectorDb
    {
        /// <summary>
        /// Save the document into named collection.
        /// If collection does not exists it is created
        /// </summary>
        Task<bool> Save(Doc doc,
                        int docIndex,
                        int parentDocId,
                        float[] vector,
                        string collectionName);

        public Task<List<SearchItem>> Search(string collectionName,
                                            ReadOnlyMemory<float> queryVector,
                                            ulong limit = 5);
    }

    public enum VectorDbProviders
    {
        QDrant,
        SQLServer,
        Elastic
    }

    public class VectorDb
    {
        public static IVectorDb? Create(VectorDbProviders providerName,
                                        string hostUrl,
                                        string providerKey)
        {
            Guard.Against.EnumOutOfRange(providerName);

            string assemblyName = string.Empty,
                   className = string.Empty,
                   assemblyVersion = string.Empty,
                   assemblyCulture = string.Empty,
                   publicKeyToken = string.Empty;
            if (providerName == VectorDbProviders.QDrant)
            {
                assemblyName = "VectorDb.QDrant";
                className = assemblyName + ".QDrantStore";
                assemblyVersion = "1.0.0.0";
                assemblyCulture = "neutral";
                publicKeyToken = "null";
            }
            else if (providerName == VectorDbProviders.SQLServer)
            {
                assemblyName = "VectorDb.SQLServer";
                className = assemblyName + ".SQLServerStore";
                assemblyVersion = "1.0.0.0";
                assemblyCulture = "neutral";
                publicKeyToken = "null";
            }

            Guard.Against.NullOrEmpty(assemblyName);
            Guard.Against.NullOrEmpty(className);

            try
            {
                string typeName = $"{className}, {assemblyName}, Version={assemblyVersion}, Culture={assemblyCulture}, PublicKeyToken={publicKeyToken}";
                Type? _type = Type.GetType(typeName);
                if (_type is null) return null;

                object[] _args = new object[]
                {
                                    new string(hostUrl),
                                    new string(providerKey)

                };
                IVectorDb? store = (IVectorDb?)Activator.CreateInstance(_type, args: _args);
                if (store is null) return null;
                return store;
            }
            catch (Exception)
            {
                return null;
            }


        }
    }
}