using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Phoenix.models
{
    internal class OpenAIEmbeddingsRequest
    {
        public string? model { get; set; }
        public string? input {  get; set; }
    }
}
