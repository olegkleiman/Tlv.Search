using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Odyssey.models;
using Odyssey.Models;
using PuppeteerSharp;
using System.Web;

namespace Odyssey
{
    public class Scrapper
    {
        private IPage       m_page;
        private SiteMap     m_siteMap;

        public Scrapper(SiteMap siteMap)
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

        public async Task<bool> Scrap(Action<Doc> callback)
        {

            foreach (var item in m_siteMap.items)
            {
                try
                {
                    Doc doc = await Scrap(item.Location);
                    callback(doc);
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            
            return true;
        }

        private async Task<Doc> Scrap(string url)
        {
            Console.WriteLine(url);

            Doc doc = new()
            {
                url = url,
                source = "sitemap0.xml"
            };

            try
            {
                await m_page.GoToAsync(url);

                // Get entire content of the page
                var content = await m_page.GetContentAsync();

                // Load the entire page into HAP
                HtmlDocument htmlDoc = new();
                htmlDoc.LoadHtml(content);

                HtmlNode htmlNode = htmlDoc.DocumentNode.SelectSingleNode(".//div[@class='DCContent']");
                if (htmlNode == null)
                    throw new ApplicationException("==> No DCContent");

                var clearText = htmlNode.InnerText.Trim();
                clearText = HttpUtility.HtmlDecode(clearText);
                doc.doc = clearText;

                htmlNode = htmlDoc.DocumentNode.SelectSingleNode(".//h1");
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
