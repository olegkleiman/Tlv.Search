using Ardalis.GuardClauses;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Odyssey.Models
{
    public class Doc(string url)
    {
        public int Id { get; set; }
        public string? Lang { get; set; }
        public string? Text { get; set; }
        public string? Description { get; set; }
        public string? Title { get; set; }
        public string? Url { get; private set; } = Guard.Against.NullOrEmpty(url);
        public string? ImageUrl { get; set; }
        public string? Source { get; set; }

        public Doc(Doc prev)
            : this(prev.Url)
        {
            Id = prev.Id;
            Lang = prev.Lang;
            Text = prev.Text;
            Title = prev.Title;
            Source = prev.Source;
        }

        public string Content
        {
            get
            {
                return Text + " " + Description + " " + Title;
            }
        }
    }
}
