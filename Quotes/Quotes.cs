using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace Quotes
{
    [ApiVersion(1,19)]
    public class Quotes : TerrariaPlugin
    {
        public override string Name { get { return "Quotes"; } }
        public override string Author { get { return "Zaicon"; } }
        public override string Description { get { return "Allows players to read/save quotes."; } }
        public override Version Version { get { return new Version(2, 3, 4, 0); } }

        List<QuoteClass> quotelist;
        private static IDbConnection db;

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
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
            }
            base.Dispose(Disposing);
        }
        #endregion

        #region Hooks
        private void OnInitialize(EventArgs args)
        {
            DBConnect();
            loadQuotes();

            Commands.ChatCommands.Add(new Command("quotes.read", DoQuotes, "quote"));
        }
        #endregion

        #region Quote Command
        private void DoQuotes(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("Invalid syntax:");
                args.Player.SendErrorMessage("{0}quote read <quote #>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                args.Player.SendErrorMessage("{0}quote search <words>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                args.Player.SendErrorMessage("{0}quote total", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                args.Player.SendErrorMessage("{0}quote random", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                if (args.Player.Group.HasPermission("quotes.mod"))
                {
                    args.Player.SendErrorMessage("{0}quote add <quote>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                    args.Player.SendErrorMessage("{0}quote <del/show> <quote #>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                }
                if (args.Player.Group.HasPermission("quotes.purge"))
                    args.Player.SendErrorMessage("{0}quote purge", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));

                return;
            }
            else if (args.Parameters.Count == 1)
            {
                if (args.Parameters[0].ToLower() == "total")
                {
                    int count = 0;

                    foreach (QuoteClass quote in quotelist)
                    {
                        if (!quote.qdeleted)
                            count++;
                    }

                    args.Player.SendMessage("There " + (count == 1 ? "is" : "are") + " " + count + " quote" + (count == 1 ? "" : "s") + " in total.", Color.LawnGreen);
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

                    args.Player.SendMessage("[" + readQuote.qtime + "] Quote #" + (randnum + 1).ToString() + " by " + readQuote.qauthor + ": " + readQuote.qquote, Color.LawnGreen);
                }
                else if (args.Parameters[0].ToLower() == "purge" && args.Player.Group.HasPermission("quotes.purge"))
                    args.Player.SendInfoMessage("Warning: This will permanently remove all deleted quotes. This will also alter the quote numbers. To continue, type {0}quote purge confirm.", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                else
                {
                    args.Player.SendErrorMessage("Invalid syntax:");
                    args.Player.SendErrorMessage("{0}quote read <quote #>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                    args.Player.SendErrorMessage("{0}quote search <words>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                    args.Player.SendErrorMessage("{0}quote total", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                    args.Player.SendErrorMessage("{0}quote random", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                    if (args.Player.Group.HasPermission("quotes.mod"))
                    {
                        args.Player.SendErrorMessage("{0}quote add <quote>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                        args.Player.SendErrorMessage("{0}quote <del/show> <quote #>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                    }
                    if (args.Player.Group.HasPermission("quotes.purge"))
                        args.Player.SendErrorMessage("{0}quote purge", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
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
                        if (readQuote.qdeleted && !args.Player.Group.HasPermission("quotes.mod"))
                        {
                            args.Player.SendErrorMessage("This quote has been deleted!");
                            return;
                        }
                        else if (!readQuote.qdeleted)
                        {
                            args.Player.SendMessage("[" + readQuote.qtime + "] Quote #" + (quotenum + 1).ToString() + " by " + readQuote.qauthor + ": " + readQuote.qquote, Color.LawnGreen);
                        }
                        else
                        {
                            args.Player.SendMessage("[Deleted][" + readQuote.qtime + "] Quote #" + (quotenum + 1).ToString() + " by " + readQuote.qauthor + ": " + readQuote.qquote, Color.DarkOrange);
                        }
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
                        if (quotelist[quotenum - 1].qauthor != args.Player.User.Name && !args.Player.Group.HasPermission("quotes.mod"))
                        {
                            args.Player.SendErrorMessage("You do not have permission to delete other users' quotes!");
                            return;
                        }

                        if (quotelist[quotenum - 1].qdeleted)
                        {
                            args.Player.SendErrorMessage("This quote has already been deleted!");
                            return;
                        }
                        quotelist[quotenum - 1].qdeleted = true;

                        delQuote(quotenum, true);

                        args.Player.SendSuccessMessage("Successfully deleted quote #{0}!", quotenum);
                    }
                }
                else if (args.Parameters[0].ToLower() == "search")
                {
                    searchQuotes(args.Player, args.Parameters[1]);
                }
                else if (args.Parameters[0].ToLower() == "show")
                {
                    if (!args.Player.Group.HasPermission("quotes.mod"))
                    {
                        args.Player.SendErrorMessage("You do not have permission to show deleted quotes.");
                        return;
                    }

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
                        QuoteClass quote = quotelist[quotenum - 1];

                        if (quote.qdeleted)
                        {
                            quote.qdeleted = false;
                            delQuote(quotenum, false);
                            args.Player.SendSuccessMessage("Restored quote #" + quotenum.ToString());
                        }
                        else
                        {
                            args.Player.SendErrorMessage("This quote is not deleted!");
                        }
                    }
                }
                else if (args.Parameters[0].ToLower() == "purge" && args.Parameters[1].ToLower() == "confirm" && args.Player.Group.HasPermission("quotes.purge"))
                {
                    purgeQuotes();
                    args.Player.SendSuccessMessage("Quotes successfully purged.");
                }
                else
                {
                    args.Player.SendErrorMessage("Invalid syntax:");
                    args.Player.SendErrorMessage("{0}quote read <quote #>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                    args.Player.SendErrorMessage("{0}quote search <words>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                    args.Player.SendErrorMessage("{0}quote total", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                    args.Player.SendErrorMessage("{0}quote random", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                    if (args.Player.Group.HasPermission("quotes.mod"))
                    {
                        args.Player.SendErrorMessage("{0}quote add <quote>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                        args.Player.SendErrorMessage("{0}quote <del/show> <quote #>", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                    }
                    if (args.Player.Group.HasPermission("quotes.purge"))
                        args.Player.SendErrorMessage("{0}quote purge", (args.Silent ? TShock.Config.CommandSilentSpecifier : TShock.Config.CommandSpecifier));
                    return;
                }
            }
            else
            {
                if (args.Parameters[0].ToLower() == "add")
                {
                    if (!args.Player.Group.HasPermission("quotes.add") && !args.Player.Group.HasPermission("quotes.mod"))
                    {
                        args.Player.SendErrorMessage("You do not have permission to add quotes.");
                        return;
                    }

                    string quote = string.Join(" ", args.Parameters);
                    quote = quote.Replace("add ", "");

                    QuoteClass newQuote = new QuoteClass(quotelist.Count + 1, args.Player.User.Name, DateTime.Now.ToString(), quote, false);

                    quotelist.Add(newQuote);
                    addQuote(quotelist.Count, newQuote);

                    args.Player.SendSuccessMessage("You have added quote #{0}!", newQuote.qid);
                    if (!args.Silent)
                        TSPlayer.All.SendMessage("{0} has added a new quote! Use {2}quote read {1} to view it!".SFormat(args.Player.Name, newQuote.qid, TShock.Config.CommandSpecifier), Color.LawnGreen);
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
       
        private bool addQuote(int newID, QuoteClass newquote)
        {
            db.Query("INSERT INTO Quotes (ID, Deleted, Author, Date, Quote) VALUES (@0, @1, @2, @3, @4)", newID, 0, newquote.qauthor, newquote.qtime, newquote.qquote);
            return true;
        }

        private void delQuote(int delID, bool deleted)
        {
            if (deleted)
                db.Query("UPDATE Quotes SET Deleted=@0 WHERE ID=@1", 1, delID);
            else
                db.Query("UPDATE Quotes SET Deleted=@0 WHERE ID=@1", 0, delID);
        }

        private void purgeQuotes()
        {
            List<QuoteClass> tobedeleted = new List<QuoteClass>();
            foreach (QuoteClass quote in quotelist)
            {
                if (quote.qdeleted)
                    tobedeleted.Add(quote);
            }
            foreach (QuoteClass quote in tobedeleted)
            {
                quotelist.Remove(quote);
            }

            db.Query("DELETE FROM quotes;");

            for (int i = 0; i < quotelist.Count; i++)
            {
                quotelist[i].qid = i + 1;
                addQuote(i+1, quotelist[i]);
            }
        }

        private void loadQuotes()
        {
            using (QueryResult reader = db.QueryReader(@"SELECT * FROM Quotes"))
            {
                while (reader.Read())
                {
                    quotelist.Add(new QuoteClass()
                    {
                        qid = reader.Get<int>("ID") - 1,
                        qauthor = reader.Get<string>("Author"),
                        qtime = reader.Get<string>("Date"),
                        qquote = reader.Get<string>("Quote"),
                        qdeleted = reader.Get<int>("Deleted") == 1 ? true : false
                    });
                }
            }
        }

        private void searchQuotes(TSPlayer player, string searchterm)
        {
            List<int> quoteresults = new List<int>();

            for (int i = 0; i < quotelist.Count; i++)
            {
                if (quotelist[i].qquote.ToLower().Contains(searchterm.ToLower()))
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
                        player.SendMessage("[" + readQuote.qtime + "] Quote #" + (quoteresults[0] + 1).ToString() + " by " + readQuote.qauthor + ": " + readQuote.qquote, Color.LawnGreen);
                    else
                        player.SendErrorMessage("No quotes matched your search.");
            }
        }
        #endregion

        #region Database
        private void DBConnect()
        {
            switch (TShock.Config.StorageType.ToLower())
            {
                case "mysql":
                    string[] dbHost = TShock.Config.MySqlHost.Split(':');
                    db = new MySqlConnection()
                    {
                        ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                            dbHost[0],
                            dbHost.Length == 1 ? "3306" : dbHost[1],
                            TShock.Config.MySqlDbName,
                            TShock.Config.MySqlUsername,
                            TShock.Config.MySqlPassword)

                    };
                    break;

                case "sqlite":
                    string sql = Path.Combine(TShock.SavePath, "Quotes.sqlite");
                    db = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
                    break;

            }

            SqlTableCreator sqlcreator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());

            sqlcreator.EnsureTableStructure(new SqlTable("Quotes",
                new SqlColumn("ID", MySqlDbType.Int32) { Primary = true, Unique = true, Length = 4 },
                new SqlColumn("Deleted", MySqlDbType.Int32) { Length = 1 },
                new SqlColumn("Author", MySqlDbType.Text) { Length = 15 },
                new SqlColumn("Date", MySqlDbType.Text) { Length = 30 },
                new SqlColumn("Quote", MySqlDbType.Text) { Length = 100 }));
        }
        #endregion
    }
}
