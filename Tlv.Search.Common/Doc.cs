using Ardalis.GuardClauses;
using System;

namespace Tlv.Search.Common
{
    public class Doc
    {
        public int Id { get; set; }
        public string? Lang { get; set; }
        public string? Text { get; set; }
        public string? Description { get; set; }
        public string? Title { get; set; }
        public string? SubTitle { get; set; }
        public string? Url { get; private set; }
        public string? ImageUrl { get; set; }
        public string? Source { get; set; }

        public Doc()
        {

        }

        public Doc(string url)
        {
            Url = Guard.Against.NullOrEmpty(url);
        }

        public List<Doc> subDocs = new ();

        public string? Content
        {
            get
            {
                return $"{Title?.Trim()} {SubTitle?.Trim()}";
            }
        }
    }
}
