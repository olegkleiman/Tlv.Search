using EmbeddingEngine.Core;
using Microsoft.Extensions.Configuration;

namespace Tlv.Search.Tests
{
    public class EmbeddingProvidersTest
    {
        private IConfigurationRoot configuration;

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
        public void HuggingFace_CreateProvier()
        {
            string? embeddingEngineKey = this.configuration["HF_KEY"];
            Assert.That(string.IsNullOrEmpty(embeddingEngineKey), Is.False);

            EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), "HUGGING_FACE");
            IEmbeddingEngine? embeddingEngine =
                EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                            providerKey: embeddingEngineKey,
                                                            string.Empty);
            Assert.That(embeddingEngine, Is.Not.Null);
            Assert.Pass();
        }

        [Test]
        public async Task HuggingFace_CanineC_GetEmbedding()
        {
            string? embeddingEngineKey = this.configuration["HF_KEY"];
            Assert.That(string.IsNullOrEmpty(embeddingEngineKey), Is.False);

            EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), "HUGGING_FACE");
            IEmbeddingEngine? embeddingEngine =
                    EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                                providerKey: embeddingEngineKey,
                                                                "google/canine-c");
            Assert.That(embeddingEngine, Is.Not.Null);

            string prompt = "Tel-Aviv Municipality";
            //ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync<float[][][]>(prompt);
            var promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync<float[][][]>(prompt);

            Assert.That(promptEmbedding.Length, Is.Not.Zero);
            var dim = promptEmbedding[0][0];
            Assert.That(dim.Length, Is.EqualTo(768));
            Assert.Pass();
        }

        [Test]
        public async Task HuggingFace_L6v2_GetEmbedding()
        {
            string? embeddingEngineKey = this.configuration["HF_KEY"];
            Assert.That(string.IsNullOrEmpty(embeddingEngineKey), Is.False);

            EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), "HUGGING_FACE");
            IEmbeddingEngine? embeddingEngine =
                    EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                                providerKey: embeddingEngineKey,
                                                                "sentence-transformers/all-MiniLM-L6-v2");
            Assert.That(embeddingEngine, Is.Not.Null);

            string prompt = "Tel-Aviv Municipality";
            ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync(prompt);

            Assert.That(promptEmbedding.Length, Is.Not.Zero);
            Assert.That(promptEmbedding.Length, Is.EqualTo(384));
            Assert.Pass();
        }

        [Test]
        public async Task HuggingFace_e5Large_GetEmbedding()
        {
            string? embeddingEngineKey = this.configuration["HF_KEY"];
            Assert.That(string.IsNullOrEmpty(embeddingEngineKey), Is.False);

            EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), "HUGGING_FACE");
            IEmbeddingEngine? embeddingEngine =
                    EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                                providerKey: embeddingEngineKey,
                                                                "intfloat/multilingual-e5-large");
            Assert.That(embeddingEngine, Is.Not.Null);

            string prompt = "Tel-Aviv Municipality";
            ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync(prompt);
            
            Assert.That(promptEmbedding.Length, Is.Not.Zero);
            Assert.That(promptEmbedding.Length, Is.EqualTo(1024));
            Assert.Pass();
        }

        [Test]
        public async Task HuggingFace_e5LargeV2_GetEmbedding()
        {
            string? embeddingEngineKey = this.configuration["HF_KEY"];
            Assert.That(string.IsNullOrEmpty(embeddingEngineKey), Is.False);

            EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), "HUGGING_FACE");
            IEmbeddingEngine? embeddingEngine =
                    EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                                providerKey: embeddingEngineKey,
                                                                "intfloat/e5-large-v2");
            Assert.That(embeddingEngine, Is.Not.Null);

            string prompt = "Tel-Aviv Municipality";
            ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync(prompt);

            Assert.That(promptEmbedding.Length, Is.Not.Zero);
            Assert.That(promptEmbedding.Length, Is.EqualTo(1024));
            Assert.Pass();
        }

        [Test]
        public async Task HuggingFace_e5Base_GetEmbedding()
        {
            string? embeddingEngineKey = this.configuration["HF_KEY"];
            Assert.That(string.IsNullOrEmpty(embeddingEngineKey), Is.False);

            EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), "HUGGING_FACE");
            IEmbeddingEngine? embeddingEngine =
                EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                            providerKey: embeddingEngineKey,
                                                            "intfloat/multilingual-e5-base");
            Assert.That(embeddingEngine, Is.Not.Null);

            string prompt = "Tel-Aviv Municipality";
            ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync(prompt);

            Assert.That(promptEmbedding.Length, Is.Not.Zero);
            Assert.That(promptEmbedding.Length, Is.EqualTo(768));
            Assert.Pass();
        }

        [Test]
        public void OpenAI_CreateProvider()
        {
            string? embeddingEngineKey = this.configuration["OPENAI_KEY"];
            Assert.That(string.IsNullOrEmpty(embeddingEngineKey), Is.False);

            EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), "OPENAI");
            IEmbeddingEngine? embeddingEngine = 
                EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                            providerKey: embeddingEngineKey,
                                                            "");
            Assert.That(embeddingEngine, Is.Not.Null);
            Assert.Pass();
        }

        [Test]
        public async Task OpenAI_ada002_GetEmbedding()
        {
            string? embeddingEngineKey = this.configuration["OPENAI_KEY"];
            Assert.That(string.IsNullOrEmpty(embeddingEngineKey), Is.False);

            EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), "OPENAI");
            IEmbeddingEngine? embeddingEngine =
                EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                            providerKey: embeddingEngineKey,
                                                            modelName: "text-embedding-ada-002");
            Assert.That(embeddingEngine, Is.Not.Null);

            string prompt = "Tel-Aviv Municipality";

            ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync(prompt);

            Assert.That(promptEmbedding.Length, Is.Not.Zero);
            Assert.That(promptEmbedding.Length, Is.EqualTo(1536));
            Assert.Pass();
        }
    }
}