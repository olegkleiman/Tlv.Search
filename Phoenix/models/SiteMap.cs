using HtmlAgilityPack;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;

namespace Phoenix.models
{
    public class SiteMapItem
    {
        public string? Location { get; set; }
        public DateTime LastModified { get; set; }

        public async Task DownloadAndSave(HttpClient httpClient, string connectionString)
        {
            try
            {
                Console.WriteLine(Location);

                try
                {
                    using (var playwright = await Playwright.CreateAsync())
                    { 
                        await using var browser = await playwright.Chromium.LaunchAsync();

                        var page = await browser.NewPageAsync();
                    }

                    //using var browserFetcher = new BrowserFetcher();
                    //await browserFetcher.DownloadAsync();
                    //var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true }, null);
                    //var page = await browser.NewPageAsync();
                    //await page.GoToAsync(this.Location);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                var request = new HttpRequestMessage(HttpMethod.Get, this.Location);
                var response = httpClient.SendAsync(request).Result;
                response.EnsureSuccessStatusCode();

                var _content = await response.Content.ReadAsStringAsync();
                HtmlDocument htmlDoc = new();
                htmlDoc.LoadHtml(_content);

                HtmlNode htmlNode = htmlDoc.DocumentNode.SelectSingleNode(".//div[@class='DCContent']");
                if (htmlNode == null)
                {
                    Console.WriteLine("==> No DCContent");
                    return;
                }

                var clearText = htmlNode.InnerText.Trim();
                clearText = HttpUtility.HtmlDecode(clearText);

                htmlNode = htmlDoc.DocumentNode.SelectSingleNode(".//div[@class='col-sm-8 contactDetailsText']");
                if (htmlNode == null)
                {
                    Console.WriteLine("==> No contactDetailsText");
                    return;
                }

                string address = htmlNode.ChildNodes[1].InnerText;
                address = HttpUtility.HtmlDecode(address);

                using var conn = new SqlConnection(connectionString);
                conn.Open();

                string query = $"insert into [dbo].[sitemap0] ([doc], [url]) values(N'{clearText}', '{this.Location}')";

                SqlCommand command = new()
                {
                    CommandText = query,
                    Connection = conn,
                    CommandType = System.Data.CommandType.Text,
                };

                command.ExecuteNonQuery();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                //throw;
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
