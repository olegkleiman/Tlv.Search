using Azure;
using HtmlAgilityPack;
using Microsoft.Playwright;
using Odyssey.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Odyssey
{
    public class Scrapper
    {
        private IPlaywright m_playwright;
        private IPage       m_page;
        private SiteMap     m_siteMap;

        public Scrapper(SiteMap siteMap)
        {
            m_siteMap = siteMap;
        }

        public async Task Init()
        {
            m_playwright = await Playwright.CreateAsync();

            var browserTypeLaunchOptions = new BrowserTypeLaunchOptions()
            {
                ExecutablePath = "C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe",
                Headless = false
            };

            var browserTask = m_playwright.Chromium.LaunchAsync(browserTypeLaunchOptions);
            var browser = await browserTask;
            m_page = await browser.NewPageAsync();
        }

        public async Task<bool> Scrap()
        {
            foreach (var item in m_siteMap.items)
            {
                await Scrap(item.Location);
            }
            
            return true;
        }

        private async Task<bool> Scrap(string url)
        {
            Console.WriteLine(url);

            try
            {
                await m_page.GotoAsync(url);

                // Get entire content of the page
                var content = await m_page.ContentAsync();

                // Load the entire page into HAP
                HtmlDocument htmlDoc = new();
                htmlDoc.LoadHtml(content);

                HtmlNode htmlNode = htmlDoc.DocumentNode.SelectSingleNode(".//div[@class='DCContent']");
                if (htmlNode == null)
                {
                    Console.WriteLine("==> No DCContent");
                    return false;
                }

                var clearText = htmlNode.InnerText.Trim();
                clearText = HttpUtility.HtmlDecode(clearText);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

            return true;
        }
    }
}
