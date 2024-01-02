using EmbeddingEngine.Core;
using System;
using System.IO;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Tlv.Search.Common;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
  
        public async Task<float[]?> Embed(Doc doc)
        {
            if (string.IsNullOrEmpty(m_modelName))
                return null;
            if (string.IsNullOrEmpty(doc.Content))
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
                            text = Regex.Replace(doc.Content, "[^\\p{L}\\d\t !@#$%^&*()_\\=+/+,<>?.:\\-`']", "")
                        }]
                    }
                };

                var options = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };


                string json = JsonSerializer.Serialize(payload, options);


                //TO-DO: "Request payload size can't exceeds the limit: 10000 bytes.",
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = $"https://generativelanguage.googleapis.com/v1beta/{m_modelName}:embedContent?key={m_providerKey}";
                HttpResponseMessage response = await httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                string respContent = await response.Content.ReadAsStringAsync();
                GeminiResponse? geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(respContent);
                return geminiResponse?.embedding?.values;
            }
            catch(Exception e)
            {
                await Console.Out.WriteLineAsync($"Error in {doc.Url}");
                return null;
            }

        }

    }
}
