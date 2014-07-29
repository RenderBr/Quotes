using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace Quotes
{
    [ApiVersion(1,16)]
    public class Quotes : TerrariaPlugin
    {
        public override string Name { get { return "Quotes"; } }
        public override string Author { get { return "Zaicon"; } }
        public override string Description { get { return "Allows players to read/save quotes."; } }
        public override Version Version { get { return new Version(1, 0, 3, 0); } }

        List<QuoteClass> quotelist;

        private Timer autosave;

        public Quotes(Main game)
            : base(game)
        {
            base.Order = 1;
        }

        #region Initialize/Dispose
        public override void Initialize()
        {
            quotelist = new List<QuoteClass>();

            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
        }

        protected override void Dispose(bool Disposing)
        {
            if (Disposing)
            {
                quoteReload();

                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
            }
            base.Dispose(Disposing);
        }
        #endregion

        #region Hooks
        private void OnInitialize(EventArgs args)
        {
            autosave = new Timer { Interval = 300000, AutoReset = true, Enabled = true };
            autosave.Elapsed += new ElapsedEventHandler(OnElapsed);

            Commands.ChatCommands.Add(new Command("quotes.read", DoQuotes, "quote"));

            loadQuotes();
        }

        private void OnElapsed(object sender, ElapsedEventArgs args)
        {
            quoteReload();
        }
        #endregion

        #region Quote Command
        private void DoQuotes(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("Invalid syntax:");
                args.Player.SendErrorMessage("/quote add <quote>");
                args.Player.SendErrorMessage("/quote read/del <quote #>");
                args.Player.SendErrorMessage("/quote search <words>");
                args.Player.SendErrorMessage("/quote total");
                args.Player.SendErrorMessage("/quote random");
                return;
            }
            else if (args.Parameters.Count == 1)
            {
                if (args.Parameters[0].ToLower() == "total")
                {
                    args.Player.SendMessage("There are " + quotelist.Count + " quotes in total.", Color.ForestGreen);
                }
                else if (args.Parameters[0].ToLower() == "random")
                {
                    if (quotelist.Count == 0)
                    {
                        args.Player.SendErrorMessage("There are no quotes to view!");
                        return;
                    }

                    Random random = new Random();
                    int randnum = random.Next(0, quotelist.Count);

                    while (quotelist[randnum].qdeleted)
                    {
                        randnum = random.Next(0, quotelist.Count);
                    }

                    QuoteClass readQuote = quotelist[randnum];

                    args.Player.SendMessage("[" + readQuote.qtime + "] Quote #" + (randnum + 1).ToString() + " by " + readQuote.qauthor + ": " + readQuote.qquote, Color.ForestGreen);
                }
                else
                {
                    args.Player.SendErrorMessage("Invalid syntax:");
                    args.Player.SendErrorMessage("/quote add <quote>");
                    args.Player.SendErrorMessage("/quote read/del <quote #>");
                    args.Player.SendErrorMessage("/quote search <words>");
                    args.Player.SendErrorMessage("/quote total");
                    args.Player.SendErrorMessage("/quote random");
                    return;
                }
            }
            else if (args.Parameters.Count == 2)
            {
                if (args.Parameters[0].ToLower() == "read")
                {
                    int quotenum = -1;
                    bool parsed = false;
                    parsed = Int32.TryParse(args.Parameters[1], out quotenum);
                    if (!parsed)
                    {
                        args.Player.SendErrorMessage("Invalid quote number!");
                        return;
                    }
                    else if (quotenum > quotelist.Count || quotenum < 1)
                    {
                        args.Player.SendErrorMessage("Invalid quote number!");
                        return;
                    }
                    else
                    {
                        quotenum--;
                        QuoteClass readQuote = quotelist[quotenum];
                        if (readQuote.qdeleted)
                        {
                            args.Player.SendErrorMessage("This quote has been deleted!");
                            return;
                        }
                        args.Player.SendMessage("[" + readQuote.qtime + "] Quote #" + (quotenum + 1).ToString() + " by " + readQuote.qauthor + ": " + readQuote.qquote, Color.ForestGreen);
                    }
                }
                else if (args.Parameters[0].ToLower() == "del")
                {
                    int quotenum = -1;
                    bool parsed = false;
                    parsed = Int32.TryParse(args.Parameters[1], out quotenum);
                    if (!parsed)
                    {
                        args.Player.SendErrorMessage("Invalid quote number!");
                        return;
                    }
                    else if (quotenum > quotelist.Count || quotenum < 1)
                    {
                        args.Player.SendErrorMessage("Invalid quote number!");
                        return;
                    }
                    else
                    {
                        if (quotelist[quotenum - 1].qauthor != args.Player.UserAccountName && !args.Player.Group.HasPermission("quotes.del"))
                        {
                            args.Player.SendErrorMessage("You do not have permission to delete other users' quotes!");
                            return;
                        }

                        if (quotelist[quotenum - 1].qdeleted == true)
                        {
                            args.Player.SendErrorMessage("This quote has already been deleted!");
                            return;
                        }
                        quotelist[quotenum - 1].qdeleted = true;

                        args.Player.SendSuccessMessage("Successfully deleted quote #{0}!", quotenum);
                    }
                }
                else if (args.Parameters[0].ToLower() == "search")
                {
                    searchQuotes(args.Player, args.Parameters[1]);
                }
                else
                {
                    args.Player.SendErrorMessage("Invalid syntax:");
                    args.Player.SendErrorMessage("/quote add <quote>");
                    args.Player.SendErrorMessage("/quote read/del <quote #>");
                    args.Player.SendErrorMessage("/quote search <words>");
                    args.Player.SendErrorMessage("/quote total");
                    args.Player.SendErrorMessage("/quote random");
                    return;
                }
            }
            else
            {
                if (args.Parameters[0].ToLower() == "add")
                {
                    if (!args.Player.Group.HasPermission("quotes.add"))
                    {
                        args.Player.SendErrorMessage("You do not have permission to add quotes.");
                        return;
                    }

                    string quote = string.Join(" ", args.Parameters);
                    quote = quote.Replace("add ", "");

                    QuoteClass newQuote = new QuoteClass(args.Player.UserAccountName, DateTime.Now.ToString(), quote, false);

                    quotelist.Add(newQuote);

                    args.Player.SendSuccessMessage("You have added quote #{0}!", quotelist.Count);
                }
                else if (args.Parameters[0].ToLower() == "search")
                {
                    string searchterms = string.Join(" ", args.Parameters);
                    searchterms = searchterms.Replace("search ", "");
                    searchQuotes(args.Player, searchterms);
                }
            }
        }
        #endregion

        #region Quote Functions
       
        void quoteReload()
        {
            if (File.Exists(@"tshock\quotes.txt"))
                File.Delete(@"tshock\quotes.txt");

            using (StreamWriter stream = new StreamWriter(@"tshock\quotes.txt", false))
            {
                string newfile = "";

                foreach (QuoteClass quote in quotelist)
                {
                    newfile += (quote.qdeleted ? "T" : "F");
                    newfile += "|";
                    newfile += quote.qtime;
                    newfile += "|";
                    newfile += quote.qauthor;
                    newfile += "|";
                    newfile += quote.qquote;

                    stream.WriteLine(newfile);
                }

                stream.Close();
            }
        }

        void loadQuotes()
        {
            quotelist = new List<QuoteClass>();

            if (File.Exists(@"tshock\quotes.txt"))
            {
                using (StreamReader stream = new StreamReader(@"tshock\quotes.txt"))
                {
                    string readline;
                    string[] quoteline;
                    QuoteClass quote;
                    bool isdeleted;
                    while (!stream.EndOfStream)
                    {
                        readline = stream.ReadLine();
                        quoteline = readline.Split('|');
                        if (quoteline[0] == "T")
                            isdeleted = true;
                        else
                            isdeleted = false;
                        quote = new QuoteClass(quoteline[1], quoteline[2], quoteline[3], isdeleted);
                        quotelist.Add(quote);
                    }

                    stream.Close();
                }
            }
        }

        private void searchQuotes(TSPlayer player, string searchterm)
        {
            List<int> quoteresults = new List<int>();

            for (int i = 0; i < quotelist.Count; i++)
            {
                if (quotelist[i].qquote.Contains(searchterm))
                {
                    quoteresults.Add(i);
                }
            }

            if (quoteresults.Count < 1)
            {
                player.SendInfoMessage("No quotes matched your search.");
            }
            else if (quoteresults.Count > 1)
            {
                string outputresults = string.Join(", ", quoteresults.Select(p => (p+1).ToString()));
                player.SendInfoMessage("Multiple quotes matched your search: " + outputresults);
            }
            else
            {
                    QuoteClass readQuote = quotelist[quoteresults[0]];
                    if (!readQuote.qdeleted)
                        player.SendMessage("[" + readQuote.qtime + "] Quote #" + (quoteresults[0] + 1).ToString() + " by " + readQuote.qauthor + ": " + readQuote.qquote, Color.ForestGreen);
                    else
                        player.SendErrorMessage("No quotes matched your search.");
            }
        }
        #endregion
    }
}
