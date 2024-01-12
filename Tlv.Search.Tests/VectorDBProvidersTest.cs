using EmbeddingEngine.Core;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tlv.Search.Common;
using VectorDb.Core;

namespace Tlv.Search.Tests
{


    public class VectorDBProvidersTest
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


        [Test(Description = "Create QDrant VectorDb client")]
        public async Task QDrant_CreateProvider()
        {
            string? qDrantHost = this.configuration["QDRANT_HOST"];
            string? qDrantKey = this.configuration["QDRANT_KEY"];
            IVectorDb ? vectorDb = VectorDb.Core.VectorDb.Create(VectorDbProviders.QDrant, qDrantHost, qDrantKey);

            Assert.That(vectorDb, Is.Not.Null); 
            Assert.Pass();
        }

        [Test(Description = "Create QDrant VectorDb client")]
        public async Task QDrant_SaveDoc()
        {
            string? qDrantHost = this.configuration["QDRANT_HOST"];
            string? qDrantKey = this.configuration["QDRANT_KEY"];
            IVectorDb? vectorDb = VectorDb.Core.VectorDb.Create(VectorDbProviders.QDrant, qDrantHost, qDrantKey);
            
            Assert.That(vectorDb, Is.Not.Null);

            Doc doc = new(new Uri("http://localhost")) // better could be empty
            {
                Text = "Test Content",
                Title = "Test Title",
               
            };

            float[] vector = { 0.0f, 0.0f };
            bool bRes = await vectorDb.Save(doc, 0, 0, vector, "myCollection");

            Assert.That(bRes, Is.True);
            Assert.Pass();
        }
    }
}
