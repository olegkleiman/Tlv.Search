using Odyssey.Models;
using System;
using System.Xml.Linq;

namespace Odyssey.models
{
    public record SiteMapItem(string Location, DateTime LastModified);

    public class SiteMap
    {
        public List<SiteMapItem>? items { get; set; }
        public string m_url { get; set; }
        public string? ContentSelector { get; set; }

        public SiteMap(Uri url)
        {
            m_url = url.OriginalString;
        }

        static public SiteMap? Parse(Uri url)
        {
            SiteMap siteMap = new(url);

            XDocument doc;
            var ns = XNamespace.Get("http://www.sitemaps.org/schemas/sitemap/0.9");

            try
            {
                if (url.Scheme == "file")
                {
                    doc = XDocument.Load(url.Host);
                }
                else
                { 
                    using (var httpClient = new HttpClient())
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, url);
                        var res = httpClient.SendAsync(request).Result;
                        res.EnsureSuccessStatusCode();
                        string xmlDoc = res.Content.ReadAsStringAsync().Result;

                        if (string.IsNullOrEmpty(xmlDoc))
                            return null;

                        doc = XDocument.Parse(xmlDoc);
                        if (doc == null)
                            return null;

                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine(ex.Message);

                Console.WriteLine($"Trying to load from {url.Segments[1]}");
                doc = XDocument.Load($"./sitemaps/{url.Segments[1]}");
                if (doc == null)
                    return null;
            }

            if (doc == null)
                return null;

            siteMap.items = (from item in doc.Root.Elements(ns + "url")
                             select new SiteMapItem(
                                 (string)item.Element(ns + "loc"),
                                 (DateTime)item.Element(ns + "lastmod"))
                             )
                            .ToList();

            return siteMap;

        }
    }
}
