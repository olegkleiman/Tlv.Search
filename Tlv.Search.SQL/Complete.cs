using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Tlv.Search
{
    public class Complete
    {
        private readonly OpenAiService _openAiService = new OpenAiService();
        private readonly ILogger<Search>? _logger;
        public Complete(ILogger<Search> log)
        {
            _logger = log;
        }
        [FunctionName("Complete")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req)
        {
            string question = req.Query["q"];
            if (string.IsNullOrEmpty(question))
            {
                return new BadRequestObjectResult("Question is required");
            }

            string historyJson = req.Query["h"].ToString() ?? "[]";
            Message[] history = JsonConvert.DeserializeObject<Message[]>(historyJson);
            if (history == null)
            {
                return new BadRequestObjectResult("History must be array type");
            }

            try
            {
                var processedQ = await _openAiService.ProcessUserInput(question, history);
                var intent = await _openAiService.DetermineIntent(question);
                if (intent == "database_query")
                {
                    //search

                }
                var responseStream = await StreamOpenAI(question, history);
                return new OkObjectResult(responseStream);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception: {ex.Message}");
                return new StatusCodeResult(500);
            }
        }


        private async Task<Stream> StreamOpenAI(string message, Message[] history)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new("Bearer", "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");

                var combinedList = new List<Message>
            {
                new Message { role = "system", content = "אתה עוזר בשפה העברית" }
            };
                combinedList.AddRange(history);

                combinedList.Add(new Message { role = "user", content = $"{message ?? "תאמר שלום"}" });

                var content = new
                {
                    model = "gpt-3.5-turbo",
                    messages = combinedList,
                    temperature = 0,
                    max_tokens = 100,
                    n = 1,
                    stream = true
                };

                var stringContent = JsonConvert.SerializeObject(content);
                var requestContent = new StringContent(stringContent, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage()
                {
                    Content = requestContent,
                    Method = HttpMethod.Post,
                    RequestUri = new Uri("https://api.openai.com/v1/chat/completions")
                };
                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                //var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", requestContent);

                var stream = await response.Content.ReadAsStreamAsync();

                return stream;
            }

        }

    }
}