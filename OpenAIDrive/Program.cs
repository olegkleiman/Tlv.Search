using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OpenAIDrive
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding();

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);
            IConfiguration config = builder.Build();

            var connectionString = config.GetConnectionString("AZURE_SQL_CONNECTIONSTRING");

            var openaiKey = config["OPENAI_KEY"];
            if( string.IsNullOrEmpty(openaiKey) )
            {
                Console.WriteLine("OpenAI key not found in configuration");
                return;
            }

            try
            {
                List<string> examplePrompts = new()
                {
                    //"איפה נמצאת עיריית תל-אביב"
                    " What is the best football club in Brazil?"
                };

                // Azure OpenAI package
                var client = new OpenAIClient(openaiKey, new OpenAIClientOptions());

                //
                // Embeddings
                //
                List<float> queryVector = new();
                EmbeddingsOptions eo = new(
                    deploymentName: "text-embedding-ada-002",
                    input: examplePrompts);

                Response<Embeddings> _response = await client.GetEmbeddingsAsync(eo);
                foreach (var item in _response.Value.Data)
                {
                    var embedding = item.Embedding;

                    for (int i = 0; i < embedding.Length; i++)
                    {
                        float value = embedding.Span[i];
                        queryVector.Add(value);
                    }
                }

                //
                // Completions
                //
                foreach (string prompt in examplePrompts)
                {
                    CultureInfo ci = new("he-IL");
                    Console.WriteLine($"Input: {prompt}", ci.Name);

                    var context = "Ronaldo plays in the best football club in Brazil. This club is called Corinthians";
                    string userMessage = $"{context}. Answer in Hebrew the following question from the text above. Q: {prompt} A:";

                    ChatCompletionsOptions cco = new ()
                    {
                        DeploymentName = "gpt-3.5-turbo-1106", //,"gpt-4"
                        Messages =
                        {
                            new ChatRequestSystemMessage(@"You are a help assistant that extracts information from user input."),
                            new ChatRequestUserMessage(userMessage)
                        },
                        Temperature = (float)0.7,
                        MaxTokens = 800,
                        NucleusSamplingFactor = (float)0.95,
                        FrequencyPenalty = 0,
                        PresencePenalty = 0,
                    };

                    Response<ChatCompletions> responseWithoutStream = await client.GetChatCompletionsAsync(cco);
                    ChatResponseMessage responseMessage = responseWithoutStream.Value.Choices[0].Message;
                    Console.WriteLine($"[{responseMessage.Role}]: {responseMessage.Content}");

                    //ChatCompletions _response = responseWithoutStream.Value;

                    //await foreach(StreamingChatCompletionsUpdate chatUpdate in client.GetChatCompletionsStreaming(cco) )
                    //{
                    //    if( chatUpdate.Role.HasValue )
                    //        Console.WriteLine($"{chatUpdate.Role.Value.ToString().ToUpperInvariant}:");

                    //    if( !string.IsNullOrEmpty(chatUpdate.ContentUpdate) )
                    //        Console.WriteLine(chatUpdate.ContentUpdate);
                    //};

                    // Legacy completions
                    CompletionsOptions ops = new()
                    {
                        MaxTokens = 800,
                        FrequencyPenalty = 0,
                        PresencePenalty = 0,
                        Temperature = 0.7f,
                        NucleusSamplingFactor = (float)0.95,
                        DeploymentName = "text-davinci-003" //"gpt-3.5-turbo-instruct" // 
                    };
                    ops.Prompts.Add(userMessage);

                    Response<Completions> response = client.GetCompletions(ops);
                    string completion = response.Value.Choices[0].Text;
                    Console.WriteLine(completion);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
