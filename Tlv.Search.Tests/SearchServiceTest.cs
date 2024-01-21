using EmbeddingEngine.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tlv.Search.Services;
using VectorDb.Core;

namespace Tlv.Search.Tests
{
    public class SearchServiceTest
    {
        private IConfigurationRoot configuration;
        private Mock<IEmbeddingEngine> embeddingEngine;

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

            embeddingEngine = new Mock<IEmbeddingEngine>();
            Assert.That(embeddingEngine, Is.Not.Null);

            string? modelName = this.configuration["EMBEDDING_MODEL_NAME"];
            Assert.That(modelName, Is.Not.Empty);

            string? embeddingProvider = this.configuration["EMBEDIING_PROVIDER"];
            Assert.That(embeddingProvider, Is.Not.Empty);

            embeddingEngine.SetupGet(x => x.ModelName).Returns(modelName);
            embeddingEngine.SetupGet(x => x.ProviderName).Returns(embeddingProvider);
        }
  
        [Test]
        public void Search()
        {
            //var promptProcessor = new Mock<IPromptProcessingService>();
            //var loggerMock = new Mock<ILogger>();

            var vectorDb = new Mock<IVectorDb>();
            Assert.That(vectorDb, Is.Not.Null);

            string _collectionNamePostFix = $"doc_parts_{embeddingEngine.Object.ProviderName}_{embeddingEngine.Object.ModelName}";
            Assert.That(_collectionNamePostFix, Is.Not.Empty);

            var collectionName = $"doc_parts_{_collectionNamePostFix}";
            SearchService searchService = new(vectorDb.Object, embeddingEngine.Object, collectionName);
            var results = searchService.Search("תשלום חוב ארנונה");
        }
    }
}
