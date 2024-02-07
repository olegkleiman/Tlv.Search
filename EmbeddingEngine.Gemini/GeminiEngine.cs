using EmbeddingEngine.Core;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EmbeddingEngine.Gemini
{
    // Request/Response objects
    class Text
    {
        public string text { get; set; }
    }

    class Content
    {
        public Text[] parts { get; set; }
    }
    class Payload
    {
        public string model { get; set; }
        public string task_type { get; set; }
        public Content content { get; set; }
    }

    class Values
    {
        public float[]? values { get; set; }
    }

    class GeminiResponse
    {
        public Values? embedding { get; set; }
    }

    public class GeminiEngine(string providerKey, string endpoint, string modelName) : IEmbeddingEngine
    {
        public string? m_providerKey { get; set; } = providerKey;
        public const string m_modelName = "models/embedding-001";
        public string m_endpoint { get; set; } = endpoint;

        public EmbeddingsProviders provider { get; } = EmbeddingsProviders.GEMINI;

        public string ModelName
        {
            get
            {
                return m_modelName;
            }
        }

        public async Task<float[]?> GenerateEmbeddingsAsync(string input,
                                                            string representation,
                                                            ILogger? logger)
        {
            if (string.IsNullOrEmpty(input))
                return null;

            try
            {
 
                using HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json; charset=utf-8");
                var payload = new Payload
                {
                    model = m_modelName,
                    //task_type = "RETRIEVAL_QUERY",
                    content = new Content()
                    {
                        parts = [new Text()
                        {
                            text = input
                        }]
                    }
                };

                var options = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };


                string jsonPayload = JsonSerializer.Serialize(payload, options);

                //TO-DO: "Request payload size can't exceeds the limit: 10000 bytes.",

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var url = $"https://generativelanguage.googleapis.com/v1beta/{m_modelName}:embedContent?key={m_providerKey}";
                HttpResponseMessage response = await httpClient.PostAsync(url, content);

                response.EnsureSuccessStatusCode();

                string respContent = await response.Content.ReadAsStringAsync();
                GeminiResponse? geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(respContent);
                return geminiResponse?.embedding?.values;
            }
            catch (Exception)
            {
                throw;
            }

        }

        public Task<T?> GenerateEmbeddingsAsync<T>(string input)
        {
            throw new NotImplementedException();
        }

    }
}