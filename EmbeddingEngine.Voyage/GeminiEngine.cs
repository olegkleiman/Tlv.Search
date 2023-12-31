﻿using EmbeddingEngine.Core;
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

    public class GeminiEngine(string providerKey) : IEmbeddingEngine
    {
        public string? m_providerKey { get; set; } = providerKey;

        public async Task<Single[]?> Embed(Doc doc)
        {
            try
            {
                using HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json; charset=utf-8");
                var payload = new Payload
                {
                    model = "models/embedding-001",
                    content = new Content()
                    {
                        parts = [new Text()
                        {
                            text = doc.Content
                        }]
                    }
                };

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/embedding-001:embedContent?key={m_providerKey}";

                //HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
                //string strPayload = JsonSerializer.Serialize(payload);
                //request.Content = new StringContent(strPayload, Encoding.UTF8, "application/json");
                //HttpResponseMessage response = await httpClient.SendAsync(request);
                HttpResponseMessage response = await httpClient.PostAsJsonAsync(url, payload);
                response.EnsureSuccessStatusCode();

                string respContent = await response.Content.ReadAsStringAsync();
                GeminiResponse? geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(respContent);
                return geminiResponse?.embedding?.values;
            }
            catch(Exception ex)
            {
                throw;
            }

        }
    }
}
