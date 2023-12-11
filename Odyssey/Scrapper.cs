using HtmlAgilityPack;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Odyssey.models;
using Odyssey.Models;
using PuppeteerSharp;
using System.Web;

namespace Odyssey
{
    public class Scrapper
    {
        private IPage?       m_page;
        private SiteMap?     m_siteMap;
        public string? ContentSelector { get; set; }
        public string? TitleSelector { get; set; }
        public string? AddressSelector { get; set; }

        private Scrapper(SiteMap siteMap)
        {
            m_siteMap = siteMap;
        }

        public async Task Init()
        {
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
                ExecutablePath = "C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe"
            });
            m_page = await browser.NewPageAsync();
        }

        public static async Task<Scrapper?> Load(int scrapperId, 
                                        SiteMap siteMap,
                                        string connectionString)
        {
            Scrapper scrapper = new(siteMap);

            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                SqlCommand cmd = new SqlCommand($"select * from [dbo].[scrappers] where id = {scrapperId}",
                                                 conn);
                SqlDataReader reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return null;

                var browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = false,
                    ExecutablePath = "C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe"
                });

                if (!reader.IsDBNull(2))
                    scrapper.ContentSelector = reader.GetString(2);
                if (!reader.IsDBNull(3))
                    scrapper.TitleSelector = reader.GetString(3);
                if( !reader.IsDBNull(4) )
                    scrapper.AddressSelector = reader.GetString(4);

                scrapper.m_page = await browser.NewPageAsync();
            }
            catch(Exception ex)
            {
                return null;
            }

            return scrapper;
        }

        public async Task<bool> Scrap(Action<Doc> callback)
        {

            foreach (var item in m_siteMap.items)
            {
                try
                {
                    Doc doc = await Scrap(item.Location, m_siteMap.m_url.ToString());
                    callback(doc);
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            
            return true;
        }

        private async Task<Doc> Scrap(string url,
                                      string source)
        {
            Console.WriteLine(url);

            Doc doc = new()
            {
                url = url,
                source = source
            };

            try
            {
                var response = await m_page.GoToAsync(url);
                if (!response.Ok)
                    throw new ApplicationException("Couldn't load page");

                // Get entire content of the page
                var content = await m_page.GetContentAsync();

                // Load the entire page into HAP
                HtmlDocument htmlDoc = new();
                htmlDoc.LoadHtml(content);

                HtmlNode? htmlNode = htmlDoc.DocumentNode.SelectSingleNode("./html");
                if( htmlNode != null 
                    && htmlNode.Attributes != null)
                {
                    doc.lang = (from attr in htmlNode.Attributes
                                where attr.Name == "lang"
                                select attr.Value).FirstOrDefault();
                }

                //HtmlNode htmlNode = htmlDoc.DocumentNode.SelectSingleNode(".//div[@class='DCContent']");
                htmlNode = htmlDoc.DocumentNode.SelectSingleNode(this.ContentSelector);

                if (htmlNode == null)
                    throw new ApplicationException("==> No DCContent");

                var clearText = htmlNode.InnerText.Trim();
                clearText = HttpUtility.HtmlDecode(clearText);
                doc.doc = clearText;

                //htmlNode = htmlDoc.DocumentNode.SelectSingleNode(".//h1");
                htmlNode = htmlDoc.DocumentNode.SelectSingleNode(this.TitleSelector);
                if (htmlNode == null)
                    throw new ApplicationException("==> No H1 element");
                clearText = htmlNode.InnerText.Trim();
                clearText = HttpUtility.HtmlDecode(clearText);
                doc.title = clearText;
            }
            catch(Exception ex)
            {
                throw new ApplicationException(ex.Message);
            }

            return doc;
        }
    }
}
