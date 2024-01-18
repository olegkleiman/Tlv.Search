using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tlv.Recall.Services
{
    public interface IPromptProcessingService
    {
        string FilterKeywords(string input, int excludeFrequency = 20);
    }
}
