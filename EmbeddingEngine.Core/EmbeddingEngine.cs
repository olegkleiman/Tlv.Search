using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;

namespace EmbeddingEngine.Core
{
    public interface IEmbeddingEngine
    {
        public EmbeddingsProviders provider { get; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="representation">
        /// "symmetric": Symmetric embeddings assume that the text to be compared is interchangeable. Usage examples for symmetric embeddings are clustering, classification, anomaly detection or visualisation tasks. "symmetric" embeddings should be compared with other "symmetric" embeddings.
        /// "document" and "query": Asymmetric embeddings assume that there is a difference between queries and documents. They are used together in use cases such as search where you want to compare shorter queries against larger documents.
        /// "query" - embeddings are optimized for shorter texts, such as questions or keywords.
        /// "document" - embeddings are optimized for larger pieces of text to compare queries against.
        /// </param>
        /// <returns></returns>
        Task<float[]?> GenerateEmbeddingsAsync(string input, 
                                                string representation = "query",
                                                ILogger? logger = null);
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
                                                string endpoint,
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

                object[] arguments = new object[] { providerKey, endpoint, modelName };
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