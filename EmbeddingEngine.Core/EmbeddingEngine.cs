using Ardalis.GuardClauses;

namespace EmbeddingEngine.Core
{
    public interface IEmbeddingEngine
    {
        Task<float[]>? GenerateEmbeddingsAsync(string input);

        public string? m_modelName { get; set; }
    }

    public enum EmbeddingsProviders
    {
        OpenAI,
        Gemini,
        Voyage
    }

    public class EmbeddingEngine
    {
        public static IEmbeddingEngine? Create(EmbeddingsProviders providerName, string providerKey, string modelName)
        {
            Guard.Against.EnumOutOfRange(providerName);

            string assemblyName = string.Empty,
                   className = string.Empty,
                   assemblyVersion = string.Empty,
                   assemblyCulture = string.Empty,
                   publicKeyToken = string.Empty;
            if (providerName == EmbeddingsProviders.OpenAI)
            {
                assemblyName = "EmbeddingEngine.OpenAI";
                className = assemblyName + ".OpenAIEngine";
                assemblyVersion = "1.0.0.0";
                assemblyCulture = "neutral";
                publicKeyToken = "null";
            }
            else if( providerName == EmbeddingsProviders .Gemini)
            {
                assemblyName = "EmbeddingEngine.Gemini";
                className = assemblyName + ".GeminiEngine";
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
                IEmbeddingEngine? engine = (IEmbeddingEngine?)Activator.CreateInstance(_type, args: [providerKey,modelName]);
                if (engine is null) return null;
                return engine;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
