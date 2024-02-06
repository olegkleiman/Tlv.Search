using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Tlv.Search.Common;
using static Google.Protobuf.Reflection.SourceCodeInfo.Types;

namespace EventsIngestor
{
    public record Event
    {
        private string baseUrl = "https://www.tel-aviv.gov.il/";

        [JsonPropertyName("summary")]
        public string? Description { get; set; }
        
        [JsonPropertyName("title")]
        public string? Title { get; set; }
        
        [JsonPropertyName("comments")]
        public string? Text { get; set; }
       
        public string? previewPage { get; set; }
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }
        
        [JsonPropertyName("cityLocation")]
        public string? CityLocation { get; set; }
        
        [JsonPropertyName("address1")]
        public string? Address { get; set; }
        public string? FullAddress
        {
            get
            {
                return $"{CityLocation}, {Address}";
            }
        }
        [JsonPropertyName("mainPicture")]
        public string? image_url { get; set; }

        [JsonPropertyName("address1_LAT")] 
        public float Lat { get; set; }

        [JsonPropertyName("address1_LON")]
        public float Lon { get; set; }

        private static HtmlNode NodeFromTag(HtmlDocument htmlDoc, 
                                    string propertyName,
                                    string tag)
        {
            htmlDoc.LoadHtml(propertyName);
            return htmlDoc.DocumentNode
                            .Descendants(tag)
                            .First();
        }

        public Doc ToDoc()
        {
            HtmlDocument htmlDoc = new();
            var htmlNode = NodeFromTag(htmlDoc, Text, "div");
            if (string.IsNullOrEmpty(htmlNode.InnerText))
                Console.WriteLine("Empty text for element");

            var _text = htmlNode.InnerText;

            htmlNode = NodeFromTag(htmlDoc, previewPage, "a");
            var hrefValue = htmlNode.Attributes["href"].Value;

            htmlNode = NodeFromTag(htmlDoc, image_url, "img");
            var src = htmlNode.Attributes["src"].Value;

            return new Doc(new Uri(baseUrl + hrefValue))
            {
                Text = _text,
                Description = this.Description,
                Title = this.Title,
                ImageUrl = baseUrl + src,
                Address = FullAddress,
                Lat = this.Lat,
                Lon = this.Lon
            };
        }
    }
}
