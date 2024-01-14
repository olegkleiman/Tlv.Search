using Ardalis.GuardClauses;

namespace EmbeddingEngine.Core
{
    public interface IEmbeddingEngine
    {
        public EmbeddingsProviders provider { get; }
        Task<float[]?> GenerateEmbeddingsAsync(string input);
        Task<T?> GenerateEmbeddingsAsync<T>(string input);

        public string ModelName { get; }
        public string ProviderName
        {
            get
            {
                return provider.ToString();
            }
        }
    }

    public enum EmbeddingsProviders
    {
        OPENAI,
        GEMINI,
        HUGGING_FACE,
        ALEPH_ALPHA
    }

    public class EmbeddingEngine
    {
        public static IEmbeddingEngine? Create(EmbeddingsProviders providerName, 
                                                string providerKey,
                                                string modelName)
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
            else if (providerName == EmbeddingsProviders.GEMINI)
            {
                assemblyName = "EmbeddingEngine.Gemini";
                className = assemblyName + ".GeminiEngine";
                assemblyVersion = "1.0.0.0";
                assemblyCulture = "neutral";
                publicKeyToken = "null";
            }
            else if (providerName == EmbeddingsProviders.HUGGING_FACE)
            {
                assemblyName = "EmbeddingEngine.HuggingFace";
                className = assemblyName + ".HuggingFaceEngine";
                assemblyVersion = "1.0.0.0";
                assemblyCulture = "neutral";
                publicKeyToken = "null";
            }
            else if( providerName == EmbeddingsProviders.ALEPH_ALPHA )
            {
                assemblyName = "EmbeddingEngine.AlephAlpha";
                className = assemblyName + ".AlephAlphaEngine";
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

                object[] arguments = new object[] { providerKey, modelName };
                IEmbeddingEngine? engine = (IEmbeddingEngine?)Activator.CreateInstance(_type, args: arguments);
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