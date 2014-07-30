using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quotes
{
    class QuoteClass
    {
        public int qid;
        public string qauthor;
        public string qtime;
        public string qquote;
        public bool qdeleted;
        public bool qlocked;

        public QuoteClass()
        {
        }
        public QuoteClass(int ID, string author, string time, string quote, bool deleted, bool locked)
        {
            qid = ID;
            qauthor = author;
            qtime = time;
            qquote = quote;
            qdeleted = deleted;
            qlocked = locked;
        }
    }
}
