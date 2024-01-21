using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdysseyFunctions
{
    public class DiscountTypesConverterDTO
    {

        public class Field
        {
            public string Caption { get; set; }
            public string InternalName { get; set; }
            public string Type { get; set; }
            public string Value { get; set; }
        }       
        public object Attachments { get; set; }
        public List<Field> Fields { get; set; }
      
    }
}
