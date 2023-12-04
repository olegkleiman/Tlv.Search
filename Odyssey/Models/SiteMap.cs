
using Microsoft.Playwright;

using System.Web;
using System.Xml;
using System.Xml.Linq;

namespace Odyssey.models
{
    public class SiteMapItem
    {
        public string? Location { get; set; }
        public DateTime LastModified { get; set; }

        public async Task Scrap()
        {
            var browserTypeLaunchOptions = new BrowserTypeLaunchOptions()
            {
                ExecutablePath = "C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe",
                Headless = false
            };

            using (var playwright = await Playwright.CreateAsync())
            {
                using (var browserTask = playwright.Chromium.LaunchAsync(browserTypeLaunchOptions))
                {
                    var browser = await browserTask;
                    var page = await browser.NewPageAsync();

                    await page.GotoAsync(Location);

                    var loc = page.Locator("//*[@id=\"contactTabContent0\"]/div/div[1]/div[7]/div/p");
                    var content = await loc.TextContentAsync();
                }
            }
        }
    };

    public class SiteMap
    {
        public List<SiteMapItem> items { get; set; }

        static public SiteMap? Parse(string xmlDoc)
        {
            if (string.IsNullOrEmpty(xmlDoc))
                return null;

            var ns = XNamespace.Get("http://www.sitemaps.org/schemas/sitemap/0.9");
            XDocument doc = XDocument.Parse(xmlDoc);
            if( doc == null )
                return null;

            SiteMap sm = new();
            sm.items = (from item in doc.Root.Elements(ns + "url")
                                         select new SiteMapItem()
                                         {
                                             Location = (string)item.Element(ns + "loc"),
                                             LastModified = (DateTime)item.Element(ns + "lastmod")
                                         }).ToList();

            return sm;
        }
    }
}
