using Ardalis.GuardClauses;
using System.Runtime.Remoting;
using Tlv.Search.Common;

namespace VectorDb.Core
{
    public interface IVectorDb
    {
        Task<bool> Save(Doc doc, ulong docIndex, ulong parentDocId, float[] vector, string collectionName);
        List<Doc> Search(string prompt);

        public string? m_providerKey { get; set; }
    }

    public enum VectorDbProviders
    {
        QDrant,
        SQLServer,
        Elastic
    }

    public class VectorDb 
    {
        public static IVectorDb? Create(VectorDbProviders providerName, string providerKey)
        {
            Guard.Against.EnumOutOfRange(providerName);

            string assemblyName = string.Empty, 
                   className = string.Empty, 
                   assemblyVersion = string.Empty,
                   assemblyCulture = string.Empty,
                   publicKeyToken = string.Empty;
            if( providerName == VectorDbProviders.QDrant )
            {
                assemblyName = "VectorDb.QDrant";
                className = assemblyName + ".QDrantStore";
                assemblyVersion = "1.0.0.0";
                assemblyCulture = "neutral";
                publicKeyToken = "null";
            }
            else if( providerName == VectorDbProviders.SQLServer )
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
                IVectorDb? store = (IVectorDb?)Activator.CreateInstance(_type, args: providerKey);
                if (store is null) return null;
                return store;
            }
            catch(Exception)
            {
                return null;
            }

            
        }
    }
}
