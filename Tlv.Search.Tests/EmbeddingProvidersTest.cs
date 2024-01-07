using EmbeddingEngine.Core;
using Microsoft.Extensions.Configuration;

namespace Tlv.Search.Tests
{
    public class Tests
    {
        protected IConfigurationRoot configuration;

        [SetUp]
        public void Setup()
        {
            // Load configuration
            this.configuration = new ConfigurationBuilder()
                .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                //.AddUserSecrets<HealthzTests>()
                .Build();
        }

        [Test]
        public void CreateOpenAI_Provider()
        {
            string? embeddingEngineKey = this.configuration["OPENAI_KEY"];
            Assert.That(string.IsNullOrEmpty(embeddingEngineKey), Is.False);

            EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), "OPENAI");
            IEmbeddingEngine? embeddingEngine = 
                EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                            providerKey: embeddingEngineKey);
            Assert.That(embeddingEngine, Is.Not.Null);
            Assert.Pass();
        }

        [Test]
        public async Task GetOpenAIEmbedding()
        {
            string? embeddingEngineKey = this.configuration["OPENAI_KEY"];
            Assert.That(string.IsNullOrEmpty(embeddingEngineKey), Is.False);

            EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), "OPENAI");
            IEmbeddingEngine? embeddingEngine =
                EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                            providerKey: embeddingEngineKey);
            Assert.That(embeddingEngine, Is.Not.Null);

            string prompt = "Tel-Aviv municipality";

            ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync(prompt);

            Assert.That(promptEmbedding.Length, Is.Not.Zero);
            Assert.Pass();
        }
    }
}