using Odyssey.Models;
using System;
using System.Xml.Linq;

namespace Odyssey.models
{
    public class SiteMapItem
    {
        public string? Location { get; set; }
        public DateTime LastModified { get; set; }

    };

    public class SiteMap
    {
        public List<SiteMapItem>? items { get; set; }
        public Uri m_url { get; set; }

        public SiteMap(Uri url)
        {
            m_url = url;
        }

        static public SiteMap? Parse(Uri url)
        {
            SiteMap siteMap = new SiteMap(url);

            XDocument doc;
            var ns = XNamespace.Get("http://www.sitemaps.org/schemas/sitemap/0.9");

            try
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
                             select new SiteMapItem()
                             {
                                 Location = (string)item.Element(ns + "loc"),
                                 LastModified = (DateTime)item.Element(ns + "lastmod")
                             }).ToList();

            return siteMap;

        }
    }
}
