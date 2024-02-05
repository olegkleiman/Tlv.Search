using EmbeddingEngine.Core;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace Tlv.Search.Tests
{
    public class EmbeddingProvidersTest
    {
        private IConfigurationRoot configuration;

        private IEmbeddingEngine? CreateEmbeddingEngine(string providerName,
                                                        string key,
                                                        string endpoint,
                                                        string modelName)
        {
            EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), providerName);
            IEmbeddingEngine? embeddingEngine =
                EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                            providerKey: key,
                                                            endpoint: endpoint,
                                                            modelName);
            return embeddingEngine;
        }

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
            string? endpoint = this.configuration["HF_ENDPOINT"];
            Assert.That(string.IsNullOrEmpty(endpoint), Is.False);

            IEmbeddingEngine? embeddingEngine = CreateEmbeddingEngine("HUGGING_FACE",
                                                                       embeddingEngineKey, 
                                                                       endpoint: endpoint,
                                                                      string.Empty);

            Assert.That(embeddingEngine, Is.Not.Null);
            Assert.That(embeddingEngine.ProviderName, Is.Not.Empty);
            Assert.That(embeddingEngine.ModelName, Is.Empty); // excpected emtpy since created with empty model name
            Assert.Pass();
        }

        [Test]
        public async Task HuggingFace_CanineC_GetEmbedding()
        {
            string? embeddingEngineKey = this.configuration["HF_KEY"];
            Assert.That(string.IsNullOrEmpty(embeddingEngineKey), Is.False);
            string? endpoint = this.configuration["HF_ENDPOINT"];
            Assert.That(string.IsNullOrEmpty(endpoint), Is.False);

            IEmbeddingEngine? embeddingEngine = CreateEmbeddingEngine("HUGGING_FACE",
                                                                        embeddingEngineKey,
                                                                        endpoint: endpoint,
                                                                        "google/canine-c");
            Assert.That(embeddingEngine, Is.Not.Null);
            Assert.That(embeddingEngine.ModelName, Is.EqualTo("google/canine-c"));//  Is.Not.Empty);

            string prompt = "Tel-Aviv Municipality";
            //ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync<float[][][]>(prompt);
            var promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync<float[][][]>(prompt);

            Assert.That(promptEmbedding.Length, Is.Not.Zero);
            var dim = promptEmbedding[0][0];
            Assert.That(dim.Length, Is.EqualTo(768));
            Assert.Pass();
        }

        [Test]
        public async Task HuggingFace_EmberV1_GetEmbedding()
        {
            string? embeddingEngineKey = this.configuration["HF_KEY"];
            Assert.That(string.IsNullOrEmpty(embeddingEngineKey), Is.False);
            string? endpoint = this.configuration["HF_ENDPOINT"];
            Assert.That(string.IsNullOrEmpty(endpoint), Is.False);

            IEmbeddingEngine? embeddingEngine = CreateEmbeddingEngine("HUGGING_FACE",
                                                                        embeddingEngineKey, 
                                                                        endpoint: endpoint,
                                                                        "llmrails/ember-v1");
            Assert.That(embeddingEngine, Is.Not.Null);
            Assert.That(embeddingEngine.ProviderName, Is.EqualTo("HUGGING_FACE"));
            Assert.That(embeddingEngine.ModelName, Is.EqualTo("llmrails/ember-v1")); 

            string prompt = "Tel-Aviv Municipality";
            var promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync<float[]>(prompt);

            Assert.That(promptEmbedding.Length, Is.Not.Zero);
            Assert.That(promptEmbedding.Length, Is.EqualTo(1024));
            Assert.Pass();
        }

        [Test]
        public async Task HuggingFace_L6V2_GetEmbedding()
        {
            string? embeddingEngineKey = this.configuration["HF_KEY"];
            Assert.That(string.IsNullOrEmpty(embeddingEngineKey), Is.False);
            string? endpoint = this.configuration["HF_ENDPOINT"];
            Assert.That(string.IsNullOrEmpty(endpoint), Is.False);

            IEmbeddingEngine? embeddingEngine = CreateEmbeddingEngine("HUGGING_FACE",
                                                                       embeddingEngineKey,
                                                                       endpoint: endpoint,
                                                                      "sentence-transformers/all-MiniLM-L6-v2");
            Assert.That(embeddingEngine, Is.Not.Null);
            Assert.That(embeddingEngine.ProviderName, Is.EqualTo("HUGGING_FACE"));
            Assert.That(embeddingEngine.ModelName, Is.EqualTo("sentence-transformers/all-MiniLM-L6-v2"));

            string prompt = "Tel-Aviv Municipality";
            ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync(prompt);

            Assert.That(promptEmbedding.Length, Is.Not.Zero);
            Assert.That(promptEmbedding.Length, Is.EqualTo(384));
            Assert.Pass();
        }

        [Test]
        public async Task HuggingFace_Multilinguale5Large_GetEmbedding()
        {
            string? embeddingEngineKey = this.configuration["HF_KEY"];
            Assert.That(string.IsNullOrEmpty(embeddingEngineKey), Is.False);
            string? endpoint = this.configuration["HF_ENDPOINT"];
            Assert.That(string.IsNullOrEmpty(endpoint), Is.False);

            IEmbeddingEngine? embeddingEngine = CreateEmbeddingEngine("HUGGING_FACE",
                                                                        embeddingEngineKey,
                                                                        endpoint: endpoint,
                                                                         "intfloat/multilingual-e5-large");
            Assert.That(embeddingEngine, Is.Not.Null);
            Assert.That(embeddingEngine.ProviderName, Is.EqualTo("HUGGING_FACE"));
            Assert.That(embeddingEngine.ModelName, Is.EqualTo("intfloat/multilingual-e5-large"));

            string prompt = "Where can I go this evening?";
            ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync(prompt);
            
            Assert.That(promptEmbedding.Length, Is.Not.Zero);
            Assert.That(promptEmbedding.Length, Is.EqualTo(1024));
            Assert.Pass();
        }

        [Test]
        public async Task HuggingFace_Local_Multilinguale5Large_GetEmbedding()
        {
            string? embeddingEngineKey = this.configuration["HF_KEY"];
            Assert.That(string.IsNullOrEmpty(embeddingEngineKey), Is.False);
            string? endpoint = this.configuration["HF_LOCAL_ENDPOINT"];
            Assert.That(string.IsNullOrEmpty(endpoint), Is.False);

            IEmbeddingEngine? embeddingEngine = CreateEmbeddingEngine("HUGGING_FACE",
                                                                        embeddingEngineKey,
                                                                        endpoint: endpoint,
                                                                         "intfloat/multilingual-e5-large");
            Assert.That(embeddingEngine, Is.Not.Null);
            Assert.That(embeddingEngine.ProviderName, Is.EqualTo("HUGGING_FACE"));
            Assert.That(embeddingEngine.ModelName, Is.EqualTo("intfloat/multilingual-e5-large"));

            string prompt = "Where can I go this evening?";
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
            string? endpoint = this.configuration["HF_ENDPOINT"];
            Assert.That(string.IsNullOrEmpty(endpoint), Is.False);

            IEmbeddingEngine? embeddingEngine = CreateEmbeddingEngine("HUGGING_FACE",
                                                                      embeddingEngineKey,
                                                                      endpoint: endpoint,
                                                                      modelName: "intfloat/e5-large-v2");
            Assert.That(embeddingEngine, Is.Not.Null);
            Assert.That(embeddingEngine.ProviderName, Is.EqualTo("HUGGING_FACE"));
            Assert.That(embeddingEngine.ModelName, Is.EqualTo("intfloat/e5-large-v2"));

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
            string? endpoint = this.configuration["HF_ENDPOINT"];
            Assert.That(string.IsNullOrEmpty(endpoint), Is.False);

            IEmbeddingEngine? embeddingEngine = CreateEmbeddingEngine("HUGGING_FACE",
                                                                    embeddingEngineKey,
                                                                    endpoint,
                                                                    "intfloat/multilingual-e5-base");
            Assert.That(embeddingEngine, Is.Not.Null);
            Assert.That(embeddingEngine.ProviderName, Is.EqualTo("HUGGING_FACE"));
            Assert.That(embeddingEngine.ModelName, Is.EqualTo("intfloat/multilingual-e5-base"));

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
            string? endpoint = this.configuration["OPENAI_ENDPOINT"];
            Assert.That(string.IsNullOrEmpty(endpoint), Is.False);
            //string? modelName = this.configuration["EMBEDDING_MODEL_NAME"];

            IEmbeddingEngine? embeddingEngine = CreateEmbeddingEngine("OPENAI", 
                                                                    embeddingEngineKey,
                                                                    endpoint: endpoint,
                                                                    modelName: string.Empty);
            Assert.That(embeddingEngine, Is.Not.Null);
            Assert.That(embeddingEngine.ProviderName, Is.EqualTo("OPENAI"));
            Assert.That(embeddingEngine.ModelName, Is.Empty); // expected emtpy since created with empty model name
            Assert.Pass();
        }

        [Test]
        public async Task OpenAI_ada002_GetEmbedding()
        {
            string? embeddingEngineKey = this.configuration["OPENAI_KEY"];
            Assert.That(string.IsNullOrEmpty(embeddingEngineKey), Is.False);
            string? endpoint = this.configuration["OPENAI_ENDPOINT"];
            Assert.That(string.IsNullOrEmpty(endpoint), Is.False);

            IEmbeddingEngine? embeddingEngine = CreateEmbeddingEngine("OPENAI", 
                                                                        embeddingEngineKey, 
                                                                        endpoint: endpoint,
                                                                        modelName: "text-embedding-ada-002");

            Assert.That(embeddingEngine, Is.Not.Null);
            Assert.That(embeddingEngine.ProviderName, Is.EqualTo("OPENAI"));
            Assert.That(embeddingEngine.ModelName, Is.EqualTo("text-embedding-ada-002"));

            string prompt = "Tel-Aviv Municipality";

            ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync(prompt);

            Assert.That(promptEmbedding.Length, Is.Not.Zero);
            Assert.That(promptEmbedding.Length, Is.EqualTo(1536));
            Assert.Pass();
        }
    }
}