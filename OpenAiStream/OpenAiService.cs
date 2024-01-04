using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

internal class OpenAiService
{

    private readonly HttpClient _httpClient;
    private readonly string _openAiKey = "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"; // Replace with your actual OpenAI API key

    public OpenAiService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiKey);
    }

    public async Task<string> DetermineIntent(string userInput)
    {
        var requestBody = new
        {
            model = "gpt-3.5-turbo-instruct",
            prompt = $"Classify the following user input as either a 'general_statement' or a 'database_query':\n\n\"{userInput}\"",
            max_tokens = 15,
            stream = false,
            temperature = 0
        };

        var response = await _httpClient.PostAsync(
            "https://api.openai.com/v1/completions",
            new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json"));

        var responseString = await response.Content.ReadAsStringAsync();
        var openAiResponse = JsonSerializer.Deserialize<OpenAiResponse>(responseString);

        var intent = openAiResponse.choices[0].text.Trim().ToLower();
        return intent.Contains("database_query") ? "database_query" : "general_statement";
    }

    public async Task<string> SummarizeWithOpenAI(string text, int maxTokens = 1000, string instructions = "Summarize the following text in Hebrew")
    {
        try
        {
            var requestBody = new
            {
                model = "gpt-3.5-turbo-instruct",
                prompt = $"{instructions}:\n\n\"{text}\"",
                max_tokens = maxTokens,
                stream = false,
                temperature = 0
            };

            var response = await _httpClient.PostAsync(
                "https://api.openai.com/v1/completions",
                new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            var openAiResponse = JsonSerializer.Deserialize<OpenAiResponse>(responseString);

            return openAiResponse?.choices[0].text.Trim();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error in OpenAI summarization: {ex.Message}");
            return "";
        }
    }

    public async Task<string> ProcessUserInput(string input, Message[] history = null)
    {
        var concatenatedHistory = string.Join(" ", history.Where(m => m.role == "user").Select(m => m.content)) + input;
        return await SummarizeWithOpenAI(concatenatedHistory, 150, "Summarize the following question in Hebrew");
    }

    public async Task<string> FrameQuestion(List<string> docTexts, string q)
    {
        var summaries = await Task.WhenAll(docTexts.Select(text => SummarizeWithOpenAI(text)));
        var combinedSummary = string.Join(" ", summaries);
        var framedQuestion = $"Based on the following information:\n\n{combinedSummary}\n\nWhat insights can we draw about:\n\n{q}";

        return framedQuestion;
    }
}

public class Message
{
    public string role { get; set; }
    public string content { get; set; }
}

public class Choice
{
    public string text { get; set; }
}

public class OpenAiResponse
{
    public List<Choice> choices { get; set; }
}

