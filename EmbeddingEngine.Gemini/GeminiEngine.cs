using EmbeddingEngine.Core;
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
    class ContentPayload
    {
        public string model { get; set; }
        public string task_type { get; set; }
        public Content content { get; set; }
    }

    class TextPayload
    {
        public string text { get; set; }
    }

    class Values
    {
        public float[]? values { get; set; }
    }

    class Value
    {
        public float[]? value { get; set; }
    }

    class GeminiResponse
    {
        public Values? embedding { get; set; }
    }

    class GeminiResponseText
    {
        public Value? embedding { get; set; }
    }

    public class GeminiEngine(string providerKey) : IEmbeddingEngine
    {
        public string? m_providerKey { get; set; } = providerKey;

        public EmbeddingsProviders provider { get; } = EmbeddingsProviders.GEMINI;

        public async Task<float[]?> GenerateEmbeddingsAsync(string input)
        {
            if (string.IsNullOrEmpty(input))
                return null;

            try
            {
                const string modelName = "models/embedding-gecko-001"; //"models/embedding-001";

                using HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json; charset=utf-8");
                //var payload = new Payload
                //{
                //    model = modelName,
                //    //task_type = "RETRIEVAL_QUERY",
                //    content = new Content()
                //    {
                //        parts = [new Text()
                //        {
                //            text = input
                //        }]
                //    }
                //};
                var payload = new TextPayload()
                {
                    text = input
                };

                var options = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };


                string jsonPayload = JsonSerializer.Serialize(payload, options);

                //TO-DO: "Request payload size can't exceeds the limit: 10000 bytes.",

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var url = $"https://generativelanguage.googleapis.com/v1beta/{modelName}:embedText?key={m_providerKey}";
                HttpResponseMessage response = await httpClient.PostAsync(url, content);

                response.EnsureSuccessStatusCode();

                string respContent = await response.Content.ReadAsStringAsync();
                GeminiResponseText? geminiResponse = JsonSerializer.Deserialize<GeminiResponseText>(respContent);
                return geminiResponse?.embedding?.value;
            }
            catch (Exception)
            {
                throw;
            }

        }

    }
}