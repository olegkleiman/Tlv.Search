using EmbeddingEngine.Core;
using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using Tlv.Search.Common;

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

    public class GeminiEngine(string providerKey, string modelName) : IEmbeddingEngine
    {
        public string? m_providerKey { get; set; } = providerKey;
        public string? m_modelName { get; set; } = modelName;

        public async Task<float[]>? GenerateEmbeddingsAsync(string input)
        {
            if (string.IsNullOrEmpty(m_modelName))
                return null;
            if (string.IsNullOrEmpty(input))
                return null;

            try
            {
                using HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json; charset=utf-8");
                var payload = new Payload
                {
                    model = m_modelName,
                    content = new Content()
                    {
                        parts = [new Text()
                        {
                            text = input
                        }]
                    }
                };

                var url = $"https://generativelanguage.googleapis.com/v1beta/{m_modelName}:embedContent?key={m_providerKey}";
                HttpResponseMessage response = await httpClient.PostAsJsonAsync(url, payload);
                response.EnsureSuccessStatusCode();

                string respContent = await response.Content.ReadAsStringAsync();
                GeminiResponse? geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(respContent);
                return geminiResponse?.embedding?.values;
            }
            catch(Exception)
            {
                throw;
            }

        }

    }
}
