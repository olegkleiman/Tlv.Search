using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Tlv.Search.Common;
using Tlv.Search.Services;

namespace Tlv.Search.Tests
{
    public class DefaultPromptProcessingTest
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
        public async Task WithRedis()
        {
            var connectionString = configuration.GetConnectionString("Redis");
            Assert.That(connectionString, Is.Not.Empty);

            var connection = ConnectionMultiplexer.Connect(connectionString);

            var processor = new DefaultPromptProcessing(connection, string.Empty);
            Assert.That(processor, Is.Not.Null);

            string prompt = "הנחות בארנוננה לחיילי מילואים";
            PromptContext promptContext = await processor.CreateContext(prompt);
            Assert.That(promptContext.FilteredPrompt, Is.Not.Null);
        }

        [Test]
        public async Task WithGeoLocation()
        {
            if (configuration is null)
                Assert.Fail();

            string openAIKey = configuration["PROMPT_PROCESSING_OPENAI_KEY"];

            var processor = new DefaultPromptProcessing(null, openAIKey);
            Assert.That(processor, Is.Not.Null);

            string prompt = "אירועי יום האהבה במרכז העיר";
            PromptContext promptContext = await processor.CreateContext(prompt);
            Assert.That(promptContext.GeoCondition, Is.Not.Null);

        }
    }
}
