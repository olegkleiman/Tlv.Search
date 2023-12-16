using System.Runtime.Remoting;

namespace VectorDb.Core
{
    public interface IVectorDb
    {
        Task<bool> Save(Doc doc);
        Task<bool> Embed(Doc doc, ulong docIndex, string key);

        public string m_providerKey { get; set; }
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

            if (string.IsNullOrEmpty(assemblyName)
                || string.IsNullOrEmpty(className))
                return null;

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
