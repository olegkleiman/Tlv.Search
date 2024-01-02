using Ardalis.GuardClauses;

namespace EmbeddingEngine.Core
{
    public interface IEmbeddingEngine
    {
        public EmbeddingsProviders provider { get; }
        Task<float[]?> GenerateEmbeddingsAsync(string input);
    }

    public enum EmbeddingsProviders
    {
        OPENAI,
        GEMINI
    }

    public class EmbeddingEngine
    {
        public static IEmbeddingEngine? Create(EmbeddingsProviders providerName, string providerKey)
        {
            Guard.Against.EnumOutOfRange(providerName);

            string assemblyName = string.Empty,
                   className = string.Empty,
                   assemblyVersion = string.Empty,
                   assemblyCulture = string.Empty,
                   publicKeyToken = string.Empty;
            if (providerName == EmbeddingsProviders.OPENAI)
            {
                assemblyName = "EmbeddingEngine.OpenAI";
                className = assemblyName + ".OpenAIEngine";
                assemblyVersion = "1.0.0.0";
                assemblyCulture = "neutral";
                publicKeyToken = "null";
            }
            else if( providerName == EmbeddingsProviders.GEMINI)
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
                IEmbeddingEngine? engine = (IEmbeddingEngine?)Activator.CreateInstance(_type, args: providerKey);
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
