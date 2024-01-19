using Microsoft.SqlServer.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tlv.Search.models
{
    enum ContextType
    {
        Geography,
        Time
    };

    class SearchContext
    {
        public ContextType   type;
        public SqlGeography? geo;
        public string?       name;
    }
}
