using HtmlAgilityPack;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using Tlv.Search.Common;

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

        private string clearText(string? _clearText)
        {
            if (string.IsNullOrEmpty(_clearText))
                return string.Empty;

            _clearText = _clearText.Trim();
            _clearText = Regex.Replace(_clearText, @"\r\n?|\n", string.Empty);
            _clearText = HttpUtility.HtmlDecode(_clearText);
            _clearText = _clearText.Replace('•', '*');
            _clearText = Regex.Replace(_clearText, "[^\\p{L}\\d\t !@#$%^&*()_\\=+/+,<>?.:\\-`']", "");
            return _clearText;
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
                Text = clearText(_text),
                Description = clearText(this.Description),
                Title = clearText(this.Title),
                ImageUrl = baseUrl + src,
                Address = FullAddress,
                Lat = this.Lat,
                Lon = this.Lon
            };
        }
    }
}
