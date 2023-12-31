using Ardalis.GuardClauses;
using BenchmarkDotNet.Attributes;
using EmbeddingEngine.Core;
using HtmlAgilityPack;
using Microsoft.Data.SqlClient;
using Microsoft.SemanticKernel.Memory;
using Odyssey.Models;
using Odyssey.Tools;
using PuppeteerSharp;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Web;
using Tlv.Search.Common;
using VectorDb.Core;

namespace Odyssey
{
    public class Scrapper 
    {
        private IBrowser?             m_browser;
        private IPage?                m_page;
        private readonly SiteMap?     m_siteMap;
        public string[]? m_ContentSelectors { get; set; }
        public string? TitleSelector { get; set; }
        public string? AddressSelector { get; set; }

        private Scrapper(SiteMap siteMap)
        {
            m_siteMap = siteMap;
        }

        public async Task Init()
        {
            m_browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
                ExecutablePath = "C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe"
            });
            
            m_page = await m_browser.NewPageAsync();
        }

        public static Scrapper? Load(int scrapperId, 
                                        SiteMap siteMap,
                                        string? connectionString)
        {
            if( string.IsNullOrEmpty(connectionString) )
                return null;

            Scrapper scrapper = new(siteMap);

            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                SqlCommand cmd = new SqlCommand($"select * from [dbo].[scrappers] where id = {scrapperId}",
                                                 conn);
                using SqlDataReader reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return null;

                if (!reader.IsDBNull(2))
                {
                    string selectors = reader.GetString(2);
                    scrapper.m_ContentSelectors = selectors.Split(';');
                }
                if (!reader.IsDBNull(3))
                    scrapper.TitleSelector = reader.GetString(3);
                if( !reader.IsDBNull(4) )
                    scrapper.AddressSelector = reader.GetString(4);
            }
            catch(Exception)
            {
                return null;
            }

            return scrapper;
        }

#pragma warning disable SKEXP0003
        public async Task<bool> ScrapTo(ISemanticTextMemory memory)
        {
            if (memory == null)
                return false;
            if (m_siteMap is null
                 || m_siteMap.items is null)
                return false;

            int docIndex = 0;
            int subDocIndex = 0;
            const string MemoryCollectionName = "site_docs2";

            foreach (SiteMapItem item in m_siteMap.items)
            {
                string docSource = m_siteMap.m_url.ToString();
                Doc? doc = await Scrap(item.Location, docSource);
                if (doc is null)
                    continue;

                doc.Id = docIndex;
                
                await memory.SaveInformationAsync(MemoryCollectionName, 
                                                    id: "info1", 
                                                    text: doc.Text);
            }

            return true;
        }
#pragma warning restore SKEXP0003

        public async Task<bool> ScrapTo(IVectorDb vectorDb,
                                        IEmbeddingEngine embeddingEngine)
        {
            if (vectorDb is null
                || embeddingEngine is null)
                return false;
            if (m_siteMap is null
                 || m_siteMap.items is null)
                return false;

            int docIndex = 0;
            int subDocIndex = 0;

            foreach (SiteMapItem item in m_siteMap.items)
            {
                string docSource = m_siteMap.m_url.ToString();
                Doc? doc = await Scrap(item.Location, docSource);
                if (doc is null)
                    continue;

                doc.Id = docIndex;

                if (doc is not null)
                {
                    //if (embeddingEngine is not null
                    //    && vectorDb is not null)
                    //{
                    //    float[] embeddings = await embeddingEngine.Embed(doc);
                    //    if (embeddings != null)
                    //        await vectorDb.Save(doc, docIndex, 0, embeddings,
                    //                            "site_docs");
                    //}
                    Console.WriteLine($"processed {docIndex}");

                    // process sub-docs
                    foreach (Doc subDoc in doc.subDocs)
                    {
                        if (embeddingEngine is not null
                            && vectorDb is not null)
                        {
                            subDoc.Title = doc.Title;
                            subDoc.Description = doc.Description;
                            subDoc.ImageUrl = doc.ImageUrl;

                            float[] embeddings = await embeddingEngine.Embed(subDoc);
                            if (embeddings != null)
                            {
                                await vectorDb.Save(subDoc, subDocIndex++, doc.Id, // parent doc id
                                                    embeddings,
                                                   "doc_parts" // collection name
                                                   );
                            }
                        }
                    }

                    docIndex++;
                }

            }

            return true;
        }

        [Benchmark]
        private async Task<Doc?> Scrap(string url,
                                      string source)
        {
            Console.WriteLine(url);
            if (m_page is null)
                return null;

            if ( m_ContentSelectors is null)
                return null;

            try
            {
                IResponse? response = await m_page.GoToAsync(url);
                if( !response.Ok )
                    throw new ApplicationException("Couldn't load page");

                // Get entire content of the page
                var content = await m_page.GetContentAsync();

                // Load the entire page into HAP
                HtmlDocument htmlDoc = new();
                htmlDoc.LoadHtml(content);

                string? lang = string.Empty, description = string.Empty,
                        imageUrl = string.Empty, title = string.Empty;

                // Get 'lang' of root html
                HtmlNode? htmlNode = htmlDoc.DocumentNode.SelectSingleNode("./html");
                if( htmlNode == null )
                    throw new ApplicationException("Couldn't select root");

                if ( htmlNode.Attributes != null)
                {
                    lang = (from attr in htmlNode.Attributes
                                where attr != null && attr.Name == "lang"
                                select attr.Value).FirstOrDefault();
                }

                OpenGraph? openGraph = new (htmlNode);
                if( openGraph is not null )
                {
                    description = openGraph.Description;
                    imageUrl = openGraph.Image;
                    title = openGraph.Title;
                }

                Doc doc = new (url)
                {
                    Source = source,
                    Lang = lang,
                    Description = description,
                    ImageUrl = imageUrl,
                    Title = title
                };

                // Get content(s)
                foreach ( var contentSelector in this.m_ContentSelectors )
                {
                    //var inputs = htmlDoc.DocumentNode.SelectNodes(".//input[contains(@class, 'filterCheckBox')]");
                    //var inputNode = inputs[1];
                    //await m_page.ClickAsync(inputNode.XPath);

                    HtmlNodeCollection htmlNodes = htmlDoc.DocumentNode.SelectNodes(contentSelector);
                    if( htmlNodes is not null )
                    {
                        Console.WriteLine($"Using {contentSelector}");
                        foreach (var node in htmlNodes)
                        {
                            string _clearText = node.InnerText.Trim();
                            _clearText = Regex.Replace(_clearText, @"\r\n?|\n", string.Empty);
                            _clearText = Regex.Replace(_clearText, @"\t", string.Empty);
                            _clearText = HttpUtility.HtmlDecode(_clearText);
                            Regex trimmer = new Regex(@"\s\s+");
                            _clearText = trimmer.Replace(_clearText, " ");
                            _clearText = _clearText.Replace('"', '\'');
                            _clearText = _clearText.Replace('•', '*');
                            doc.Text += " " + _clearText;

                            doc.subDocs.Add(new Doc(url)
                            {
                                Text = _clearText,
                            });
                        }

                        break;
                    }
                    
                }

                return doc;

            }
            catch(Exception ex)
            {
                throw new ApplicationException(ex.Message);
            }
        }


    }
}
