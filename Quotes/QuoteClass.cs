using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quotes
{
    class QuoteClass
    {
        public string qauthor;
        public string qtime;
        public string qquote;
        public bool qdeleted;

        public QuoteClass(string author, string time, string quote, bool deleted)
        {
            qauthor = author;
            qtime = time;
            qquote = quote;
            qdeleted = deleted;
        }
    }
}
