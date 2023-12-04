using Microsoft.Playwright;
using Odyssey.models;

namespace Odyssey
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            

            //
            // Process sitemaps
            //
            SiteMap? siteMap = null;
            using (var httpClient = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://www.tel-aviv.gov.il/sitemap0.xml");
                string xmlDoc = httpClient.SendAsync(request).Result.Content.ReadAsStringAsync().Result;

                siteMap = SiteMap.Parse(xmlDoc);
                if (siteMap == null)
                    return; 
                
                Scrapper scrapper = new Scrapper(siteMap);
                await scrapper.Init();
                await scrapper.Scrap();


                foreach (var item in siteMap.items)
                {
                    await item.Scrap();
                }
            }
        }


    }
}
