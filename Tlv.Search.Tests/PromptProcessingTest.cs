using EmbeddingEngine.Core;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tlv.Search.Services;

namespace Tlv.Search.Tests
{
    public class PromptProcessingTest
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
        public async Task FrequencyFilterPromptProcessing()
        {
            var connectionString = configuration.GetConnectionString("Redis");
            Assert.That(connectionString, Is.Not.Empty);

            var connection = ConnectionMultiplexer.Connect(connectionString);
            var processor = new FrequencyFilterPromptProcessing(connection);
            Assert.That(processor, Is.Not.Null);

            await processor.FilterKeywords("הנחות לארנוננה לחיילי מילואים");
        }
    }
}
