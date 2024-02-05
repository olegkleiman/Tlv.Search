using EmbeddingEngine.Core;
using RestSharp;
using System.Net;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;
using System.Threading.Tasks;

namespace EmbeddingEngine.AlephAlpha
{
    public class AlephAlphaResponse
    {
        public string model_version { get; set; }
        public float[] embedding { get; set; }
    }

    public class AlephAlphaEngine : IEmbeddingEngine
    {
        public string m_providerKey { get; set; }
        public string m_endpoint { get; set; }
        public string m_modelName { get; set; }
        public EmbeddingsProviders provider { get; } = EmbeddingsProviders.ALEPH_ALPHA;

        public string ModelName
        {
            get
            {
                return m_modelName;
            }
        }

        public AlephAlphaEngine(string providerKey,
                                string endpoint,
                                string modelName)
        {
            m_providerKey = providerKey;
            m_endpoint = endpoint;
            m_modelName = modelName;
        }

        /// <summary>
        /// Embeds a prompt using a specific model and semantic embedding method. 
        /// </summary>
        /// <param name="prompt"></param>
        /// <param name="prompt">
        /// Type of embedding representation to embed the prompt with.
        /// "symmetric": Symmetric embeddings assume that the text to be compared is interchangeable.Usage examples for symmetric embeddings are clustering, classification, anomaly detection or visualisation tasks. "symmetric" embeddings should be compared with other "symmetric" embeddings.
        /// "document" and "query": Asymmetric embeddings assume that there is a difference between queries and documents. They are used together in use cases such as search where you want to compare shorter queries against larger documents.
        /// "query"-embeddings are optimized for shorter texts, such as questions or keywords.
        /// "document"-embeddings are optimized for larger pieces of text to compare queries against.
        /// </param>
        /// <returns></returns>
        public async Task<float[]?> GenerateEmbeddingsAsync(string prompt,
                                                            string representation = "query")
        {
            var options = new RestClientOptions()
            {
                MaxTimeout = -1,
            };

            var client = new RestClient();
            var request = new RestRequest("https://api.aleph-alpha.com/semantic_embed", Method.Post);

            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Accept", "application/json");
            request.AddHeader("Authorization", $"Bearer {m_providerKey}");

            // The default behavior is to return the full embedding with 5120 dimensions.
            // With this parameter you can compress the returned embedding to 128 dimensions.
            // The compression is expected to result in a small drop in accuracy performance (4-6%),
            // with the benefit of being much smaller, which makes comparing these embeddings much faster for use cases
            // where speed is critical.
            // With the compressed embedding can also perform better if you are embedding really short texts or documents.
            var body = @"{" + "\n" +
            $@"  ""model"": ""{m_modelName}""," + "\n" +
            $@"  ""prompt"": ""{prompt}""," + "\n" +
            @"  ""representation"": ""symmetric""," + "\n" +
            @"  ""normalize"": true," + "\n" +
            @"  ""compress_to_size"": 128" + "\n" +
            @"}";

            request.AddStringBody(body, DataFormat.Json);

            RestResponse response = await client.ExecuteAsync(request);
            var aaResponse = JsonSerializer.Deserialize<AlephAlphaResponse>(response.Content);
            return aaResponse.embedding;
        }

        /// <summary>
        /// Embeds a text using a specific model. 
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        //public async Task<float[]?> GenerateEmbeddingsAsync(string input)
        //{
        //    var options = new RestClientOptions()
        //    {
        //        MaxTimeout = -1,
        //    };

        //    var client = new RestClient();
        //    var request = new RestRequest("https://api.aleph-alpha.com/semantic_embed", Method.Post);

        //    request.AddHeader("Content-Type", "application/json");
        //    request.AddHeader("Accept", "application/json");
        //    request.AddHeader("Authorization", $"Bearer {m_providerKey}");

        //    // The default behavior is to return the full embedding with 5120 dimensions.
        //    // With this parameter you can compress the returned embedding to 128 dimensions.
        //    // The compression is expected to result in a small drop in accuracy performance (4-6%),
        //    // with the benefit of being much smaller, which makes comparing these embeddings much faster for use cases
        //    // where speed is critical.
        //    // With the compressed embedding can also perform better if you are embedding really short texts or documents.
        //    var body = @"{" + "\n" +
        //    $@"  ""model"": ""{m_modelName}""," + "\n" +
        //    $@"  ""prompt"": ""{input}""," + "\n" +
        //    @"  ""layers"": ""[0,1]," + "\n" +
        //    @"  ""tokens"": false," + "\n" +
        //    @"  ""normalize"": true," + "\n" +
        //    @"  ""pooling"":  [""max""]," + "\n" +
        //    //@"  ""type"": ""asymmetric_document"" + 
        //    @"}";

        //    request.AddStringBody(body, DataFormat.Json);

        //    RestResponse response = await client.ExecuteAsync(request);
        //    var aaResponse = JsonSerializer.Deserialize<AlephAlphaResponse>(response.Content);
        //    return aaResponse.embedding;
        //}

        public Task<T?> GenerateEmbeddingsAsync<T>(string input)
        {
            throw new NotImplementedException();
        }


    }
}
