using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;

namespace Odyssey.Tools
{
    internal record OpenGraph(HtmlNode root)
    {
        private const string attributeProperty = "property";
        private const string attributeContent = "content";

        public string? Description
        {
            get
            {
                return getPropertyContent("og:description");
            }
        }
        public string? Image
        {
            get
            {
                return getPropertyContent("og:image");
            }
        }
        public string? Title
        {
            get
            {
                return getPropertyContent("og:title");
            }
        }

        private string? getPropertyContent(string property)
        {
            string? val = (from node in metaNodes
                           where node.Attributes[attributeProperty] is not null
                                && node.Attributes[attributeProperty].Value == property
                                select node.Attributes[attributeContent]?.Value)
                            .FirstOrDefault();

            return string.IsNullOrEmpty(val) ?
                val :
                HttpUtility.HtmlDecode(val.Trim());
        }

        private HtmlNodeCollection? metaNodes = root.SelectNodes(".//meta");
    }
}
