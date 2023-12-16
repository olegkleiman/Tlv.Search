﻿using HtmlAgilityPack;
using Microsoft.Data.SqlClient;
using Odyssey.Models;
using Odyssey.Tools;
using PuppeteerSharp;
using System.Web;
using VectorDb.Core;

namespace Odyssey
{
    public class Scrapper
    {
        private IPage?                m_page;
        private readonly SiteMap?     m_siteMap;
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

        public async Task<bool> Scrap(Action<Doc?> callback)
        {
            if (m_siteMap is null
                 || m_siteMap.items is null )
                return false;

            foreach (SiteMapItem item in m_siteMap.items)
            {
                try
                {
                    string docSource = m_siteMap.m_url.ToString();
                    Doc? doc = await Scrap(item.Location, docSource);
                    if( doc is not null)
                        callback(doc);
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            
            return true;
        }

        public async Task<bool> ScrapTo(IVectorDb vectorDb, string embeddingKey)
        {
            if (vectorDb is null)
                return false;
            if (m_siteMap is null
                 || m_siteMap.items is null)
                return false;

            ulong docIndex = 0;
            foreach (SiteMapItem item in m_siteMap.items)
            {
                string docSource = m_siteMap.m_url.ToString();
                Doc? doc = await Scrap(item.Location, docSource);
                
                if (doc is not null)
                {
                    //Task task = vectorDb.Save(doc)
                    //    .ContinueWith((task) =>
                    //            vectorDb.Embed(doc, docIndex++, embeddingKey)
                    //    );
                    //await task;
                    await vectorDb.Save(doc);
                    await vectorDb.Embed(doc, docIndex++, embeddingKey);
                }
                Console.WriteLine(docIndex);
            }

            return true;
        }

        private async Task<Doc?> Scrap(string url,
                                      string source)
        {
            Console.WriteLine(url);
            if (m_page is null)
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
                HtmlNodeCollection htmlNodes = htmlDoc.DocumentNode.SelectNodes(this.ContentSelector);
                if (htmlNodes == null || htmlNodes.Count == 0)
                    throw new ApplicationException($"==> No {this.ContentSelector} for content");
                foreach (var node in htmlNodes)
                {
                    string _clearText = node.InnerText.Trim();
                    _clearText = HttpUtility.HtmlDecode(_clearText);
                    doc.Text += " " + _clearText;
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