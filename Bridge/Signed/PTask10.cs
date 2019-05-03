/*
 * * LEGAL DISCLAIMER * *

The currency markets can do ANYTHING at ANY TIME.
No one can guarantee or forecast how these results will perform or behave in future markets.
Anyone who uses this product or this information is responsible for deciding If, Where, When and How this product and information are applied.
Anyone who uses this product or this information is responsible and liable for any outcomes that might result from the use of this product or this information.
There is no warranty or guarantee provided or implied for this product or this information for any purpose.

 */

using System;
using System.ComponentModel;
//using System.Windows.Media;
using Alveo.Interfaces.UserCode;
using Alveo.UserCode;
using Alveo.Common;
using Alveo.Common.Classes;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.IO.Pipes;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Alveo.UserCode
{
    [Serializable]
    [Description("")]
    public class PTask10 : ExpertAdvisorBase
    {

        #region Properties
        [Category("Settings")]
        [Description("Fixed Stoploss limit in Pips. Value=0, use MT4 Stoploss. [ex: 20]")]
        [DisplayName("Fixed Stoploss(Pips)")]
        public int fixedStoploss { get; set; }

        [Category("Settings")]
        [Description("Percent of account balance used for StopLoss limit. Value=0, use MT4 Stoploss. [ex: 1.5]")]
        [DisplayName("Percent Risk(percent)")]
        public double pctRisk { get; set; }

        [Category("Settings")]
        [Description("Ratio of MT4 Lotsize for Alveo. If value<=0, ratio is 1.0. [ex: 0.5]")]
        [DisplayName("Lotsize ratio")]
        public double ratioLotSize { get; set; }

        [Category("Settings")]
        [Description("Fixed Lotsize for Alveo. If value > 0, this lotsize will be used for all Alveo orders. [ex: 0.01]")]
        [DisplayName("Fized Lotsize")]
        public double fixedLotSize { get; set; }
        #endregion

        String symbol;
        internal Object msgLock = null;
        internal volatile Queue<string> msgs;
        internal Dictionary<int, Trade> trades;
        internal List<Trade> Alveotrades;
        Dictionary<int, Trade> record;
        DateTime initTime;
        internal Object TLock;                                      // Lock for TradeLog file access
        internal string dataFileDir;                                //directory for data files
        bool ableToTrade;
        DateTime prevTime;
        string pipename;
        NamedPipeServerStream server;
        //CancellationTokenSource cts;
        bool abortFlag;
        Dictionary<int, int> Atrades;
        bool busy;
        DateTime lastOpen;
        Dictionary<string, double> pipValues;
        double pipvalue;

        public PTask10()
        {
            // Basic EA initialization. Don't use this constructor to calculate values
            copyright = "(C) 2019 Entity3 LLC, All rights reserved.";
            link = "";
            pipename = "";
            server = null;
            fixedStoploss = 0;
            pctRisk = 0;
            ratioLotSize = 0;
            fixedLotSize = 0;

            //cts = null;
        }
        //+------------------------------------------------------------------+"
        //| expert initialization function                                   |"
        //+------------------------------------------------------------------+"
        protected override int Init()
        {
            try
            {
                //cts = new CancellationTokenSource();
                initTime = DateTime.UtcNow;
                symbol = Symbol();
                abortFlag = false;
                msgLock = new Object();
                msgs = new Queue<string>();
                trades = new Dictionary<int, Trade>();
                Atrades = new Dictionary<int, int>();
                Alveotrades = new List<Trade>();
                record = new Dictionary<int, Trade>();
                var tf = Chart.TimeFrame;
                dataFileDir = "C:\\temp\\PTask10 " + symbol.Replace("/", "") + " " + tf.ToString() + "\\";       //directory for data files
                ableToTrade = true;
                TLock = null;
                prevTime = DateTime.Now;
                busy = false;
                lastOpen = DateTime.MinValue;
                LoadPipValues();
                pipvalue = 1.0;
                if (pipValues.Count > 0)
                    pipValues.TryGetValue(symbol, out pipvalue);

                StartServer(symbol);
                GetMtrades();
                LogPrint("Server Started.");
            }
            catch (Exception e)
            {
                LogPrint(e.Message);
                LogPrint(e.StackTrace);
            }
            return 0;
        }

        //+------------------------------------------------------------------+"
        //| expert deinitialization function                                 |"
        //+------------------------------------------------------------------+"
        protected override int Deinit()
        {
            try
            {
                LogPrint("Deinit");
                //cts.Cancel();
                abortFlag = true;
                if (pipename.Length > 0)
                {
                    LogPrint("Deinit: terminating pipename=" + pipename);
                    using (NamedPipeClientStream npcs = new NamedPipeClientStream(".", pipename, PipeDirection.Out))
                    {
                        npcs.Connect(100);
                    }
                }
                System.Threading.Thread.Sleep(1000);
                if (server != null)
                {
                    server.Dispose();
                    server = null;
                }
                //cts.Dispose();
                LogPrint("PTask10 v 1.0 Done.");
            }
            catch (Exception e)
            {
                LogPrint(e.Message);
                LogPrint(e.StackTrace);
            }
            return 0;
        }

        bool IsAbleToTrade()
        {
            var dt = DateTime.UtcNow;
            var dt2 = DateTime.Now;
            bool isAbleToTrade = true;
            if (!IsConnected())
            {
                if (ableToTrade)
                    LogPrint("Start: !IsConnected.");
                isAbleToTrade = false;
            }
            else if (!IsTradeAllowed())
            {
                if (ableToTrade)
                    LogPrint("Start: !IsTradeAllowed.");
                isAbleToTrade = false;
            }
            else if (isStopped)
            {
                if (ableToTrade)
                    LogPrint("Start: isStopped.");
                isAbleToTrade = false;
            }
            else if (dt.DayOfWeek == System.DayOfWeek.Saturday
                || (dt.DayOfWeek == System.DayOfWeek.Friday && dt.TimeOfDay > new TimeSpan(23, 0, 0))
                || (dt.DayOfWeek == System.DayOfWeek.Sunday && dt.TimeOfDay < new TimeSpan(22, 0, 0)))
            {
                if (ableToTrade)
                    LogPrint("Start: Markets closed.");
                isAbleToTrade = false;
            }
            else if (dt2.TimeOfDay > new TimeSpan(12 + 4, 45, 0) && dt2.TimeOfDay < new TimeSpan(12 + 5, 20, 0))
            {
                if (ableToTrade)
                    LogPrint("Start: Alveo maintenance period.");
                isAbleToTrade = false;
            }
            return isAbleToTrade;
        }

        //+------------------------------------------------------------------+"
        //| expert start function                                            |"
        //+------------------------------------------------------------------+"
        protected override int Start()
        {
            try
            {
                var dt = DateTime.Now;
                var dur = dt.Subtract(prevTime);
                if (busy || dur < new TimeSpan(0, 0, 1))
                {
                    Sleep(50);
                    return 0;
                }
                prevTime = dt;

                var able = IsAbleToTrade();
                if (ableToTrade != able)
                {
                    ableToTrade = able;
                    if (!ableToTrade)
                    {
                        LogPrint("Start: !ableToTrade.");
                        return 0;
                    }
                    else
                    {
                        LogPrint("Start: trading resumed.");
                    }
                }
                string msg = "";
                lock (msgLock)
                {
                    int count = msgs.Count;
                    if (count > 0)
                    {
                        msg = msgs.Dequeue();
                    }
                }
                busy = true;
                trades.Clear();
                if (msg.Length > 0)
                    HandleMsg(msg);
                if (trades.Count > 0)
                {
                    LogPrint("Start: trades=" + trades.Count);
                    foreach (var mtrade in trades.Values)
                    {
                        string oper = mtrade.Oper;
                        LogPrint("Start: mtrade ID=" + mtrade.Id + " Oper=" + oper);
                        switch (oper)
                        {
                            case "open":
                                OpenTrade(mtrade);
                                break;
                            case "close":
                                CloseTrade(mtrade);
                                break;
                            case "modify":
                                ModifyTrade(mtrade);
                                break;
                            default:
                                LogPrint("Start: Oper invalid. mtrade ID=" + mtrade.Id + " Oper=" + oper);
                                break;
                        }
                        LogPrint("Start2: trades=" + trades.Count);
                    }
                    LogPrint("Start2: handled. trades=" + trades.Count);
                }
            }
            catch (Exception e)
            {
                LogPrint(e.Message);
                LogPrint(e.StackTrace);
            }
            busy = false;
            return 0;
        }

        internal void ModifyTrade(Trade mTrade)
        {
            GetMtrades();
            int AtradeId = -1;
            Atrades.TryGetValue(mTrade.Id, out AtradeId);
            if (AtradeId > 0)
            {
                int total = OrdersTotal();
                if (OrderSelect(AtradeId, SELECT_BY_TICKET))
                {
                    LogPrint("ModifyTrade: Atrade found Id=" + AtradeId);
                    var dt = (DateTime)OrderCloseTime();
                    var com = OrderCommission();
                    var prc = OrderOpenPrice();
                    if (dt == null || dt.Year < 1980)  // order still open
                    {
                        if (com <= 0)
                            prc = mTrade.OpenPrice;  // order still Pending, update OpenPrice
                        else
                            LogPrint(" ModifyTrade: Cannot modify OpenPrice because mTrade is Filled !! mTrade.Id =" + mTrade.Id);
                        var price = NormalizeDouble(prc, Digits);
                        var sl = NormalizeDouble(mTrade.StopLoss, Digits);
                        var tp = NormalizeDouble(mTrade.TakeProfit, Digits);
                        datetime exp = null;
                        if (mTrade.ExpirationDate != null)
                        {
                            if (mTrade.ExpirationDate.Value.Year > 1980)
                                exp = (datetime)mTrade.ExpirationDate;
                        }
                        if (!OrderModify(AtradeId, price, sl, tp, exp))
                        {
                            var err = GetLastError();
                            LogPrint("CloseTrade: OrderModify failed. Id=" + AtradeId + " MT4.Id=" + mTrade.Id + " reason=" + err);
                            LogPrint("CloseTrade: OrderModify failed. price=" + price + " sl=" + sl + " tp=" + tp + " exp=" + exp.ToString());
                            if (err == 3)
                                LogPrint("Id=" + AtradeId + " price=" + price + " sl=" + sl + " tp=" + tp + " exp =" + exp.ToString());
                        }
                        else
                        {
                            LogPrint("CloseTrade: OrderModify successful. Id=" + AtradeId + " MT4.Id=" + mTrade.Id);
                            LogPrint("Id=" + AtradeId + " price=" + price + " sl=" + sl + " tp=" + tp + " exp =" + exp.ToString());
                        }
                    }
                    else
                    {
                        LogPrint("CloseTrade: Atrade already closed. Id=" + AtradeId + " MT4.Id=" + mTrade.Id);
                    }
                }
                else
                {
                    LogPrint("ModifyTrade: !! Atrade not found by OrderSelect. Atrades.Count=" + Atrades.Count + " mId = " + mTrade.Id + " AId=" + AtradeId);
                    Atrades.Remove(mTrade.Id);
                }
            }
            else
            {
                LogPrint("ModifyTrade: !! Atrade not found in Atrades. Atrades.Count=" + Atrades.Count + " mId=" + mTrade.Id);
            }
        }

        internal void GetMtrades()
        {
            Atrades.Clear();                        // clear Dictionary
            string symbol = Symbol();               // get chart Symbol
            int total = OrdersTotal();              // get total number of Alveo trades
            // write open orders
            for (int pos = 0; pos < total; pos++)   // seclect each Alveo order
            {
                if (OrderSelect(pos, SELECT_BY_POS) == false)
                    continue;
                string sym = OrderSymbol();
                if (sym != symbol)                  // not for this Chart
                    continue;
                int ticket = OrderTicket();         // get Alveo order ticket
                string cmnt = OrderComment();       // get Alveo order comment
                if (!cmnt.Contains("MT4"))          // order not from MT4
                    continue;
                // cmnt from MT4 looks like "MT4,37292436,count=1"
                string[] separators = new string[] { "," };  // separate comma separetaed terms in comment
                string[] terms = cmnt.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                int count = terms.Length;
                if (count < 2)                              // not enough terms
                    continue;
                int mID = -1;
                if (!int.TryParse(terms[1], out mID))       // get MT4 order ID from 2nd term
                    continue;
                if (mID <= 0 || mID > 1e10)                 // if TrParse failed or mID not valid
                    continue;
                Atrades.Add(mID, ticket);                   // add ticket to Atrades Dictionary
            }
            LogPrint("GetMtrades: Atrades.count=" + Atrades.Count);
        }

        internal void CloseTrade(Trade mTrade)
        {
            GetMtrades();
            int AtradeId = -1;
            Atrades.TryGetValue(mTrade.Id, out AtradeId);
            if (AtradeId > 0)
            {
                if (OrderSelect(AtradeId, SELECT_BY_TICKET))
                {
                    LogPrint("CloseTrade: Atrade found Id=" + AtradeId);
                    var dt = (DateTime)OrderCloseTime();
                    if (dt == null || dt.Year < 1980)
                    {
                        var openPrice = OrderOpenPrice();
                        var type = OrderType();
                        var vol = OrderLots();
                        if (openPrice > 0) // filled
                        {
                            LogPrint("CloseTrade: Atrade filled.");
                            double price = (type == OP_BUY) ? Bid : Ask;
                            OrderClose(AtradeId, vol, price, 0);
                            LogPrint("Start: Order Closed. Id=" + AtradeId + " MT4.Id=" + mTrade.Id);
                        }
                        else // not filled
                        {
                            LogPrint("CloseTrade: Atrade not filled");
                            OrderDelete(AtradeId);
                            LogPrint("Start: OrderDelete. Id=" + AtradeId + " MT4.Id=" + mTrade.Id);
                        }
                    }
                    else    // closed
                    {
                        LogPrint("CloseTrade: Atrade already closed. Id=" + AtradeId + " MT4.Id=" + mTrade.Id);
                        var dtNow = DateTime.UtcNow;
                        var dur = dt.Subtract(dtNow);
                        if (dur > TimeSpan.FromDays(7))
                        {
                            Atrades.Remove(mTrade.Id);
                        }
                    }
                }
                else
                {
                    LogPrint("CloseTrade: !! Atrade not found by OrderSelect. Atrades.Count=" + Atrades.Count + " mId = " + mTrade.Id + " AId=" + AtradeId);
                    Atrades.Remove(mTrade.Id);
                }
            }
            else
            {
                LogPrint("CloseTrade: !! Atrade not found in Atrades. Atrades.Count=" + Atrades.Count + " mId=" + mTrade.Id);
            }
        }

        internal void OpenTrade(Trade mTrade)
        {
            int x;
            if (Atrades.TryGetValue(mTrade.Id, out x))
            {
                LogPrint("OpenTrade: Order already exists !! ID=" + mTrade.Id);
                return;
            }
            int cmd = int.MinValue;
            double price = 0;
            double qty = mTrade.Quantity;
            if (ratioLotSize > 0)
                qty = Math.Max(ratioLotSize * mTrade.Quantity, 0.01);
            if(fixedLotSize > 0)
                qty = Math.Max(fixedLotSize, 0.01);
            qty = Math.Round(qty, 2);
            string cmnt = "MT4," + mTrade.Id.ToString() + "," + mTrade.Comment;
            if (qty <= 0)
            {
                LogPrint("OpenTrade: Qty=" + qty.ToString("F2"));
                return;
            }
            LogPrint("OpenTrade: mTrade.Side=" + (mTrade.Side.ToString()));
            if (mTrade.Side == TradeSide.Buy)
            {
                switch (mTrade.Type)
                {
                    case TradeType.Market:
                        cmd = OP_BUY;
                        price = Ask;
                        break;
                    case TradeType.Stop:
                        cmd = OP_BUYSTOP;
                        price = mTrade.OpenPrice;
                        break;
                    case TradeType.Limit:
                        cmd = OP_BUYLIMIT;
                        price = mTrade.OpenPrice;
                        break;
                }
            }
            else  // TradeSide.Sell
            {
                switch (mTrade.Type)
                {
                    case TradeType.Market:
                        cmd = OP_SELL;
                        price = Bid;
                        break;
                    case TradeType.Stop:
                        cmd = OP_SELLSTOP;
                        price = mTrade.OpenPrice;
                        break;
                    case TradeType.Limit:
                        cmd = OP_SELLLIMIT;
                        price = mTrade.OpenPrice;
                        break;
                }
            }
            double accountbalamce = AccountBalance();
            double mult = (mTrade.Side == TradeSide.Buy) ? 1 : -1;
            double stoploss = mTrade.StopLoss;
            double takeprofit = mTrade.TakeProfit;
            if (fixedStoploss > 0)
            {
                stoploss = price - mult * (double)fixedStoploss * 10 * Point;
                LogPrint("OpenTrade: stoploss=" + stoploss + " fixedStoploss=" + fixedStoploss);
            }
            else if (pctRisk > 0)
            {
                double pips = (pctRisk / 100) * accountbalamce / pipvalue;
                stoploss = price - mult * Math.Max(pips, 2.0) * 10 * Point;
                LogPrint("OpenTrade: stoploss=" + stoploss + " pctRisk=" + pctRisk);
            }
            else
            {
                LogPrint("OpenTrade: stoploss=" + stoploss + " mTrade.StopLoss=" + mTrade.StopLoss);
            }
            stoploss = NormalizeDouble(stoploss, Digits);
            LogPrint("OpenTrade: mTrade.Type=" + (mTrade.Type.ToString()) + " stoploss=" + stoploss);
            int ticket = -1;
            if (mTrade.Type == TradeType.Market)
            {
                ticket = SendOrder(cmd, qty, price, stoploss, takeprofit, cmnt);
            }
            else
            {
                if (mTrade.ExpirationDate == null || mTrade.ExpirationDate.Value.Year < 1980)
                    ticket = SendOrder(cmd, qty, price, stoploss, takeprofit, cmnt);
                else
                {
                    datetime expr = mTrade.ExpirationDate;
                    ticket = SendOrder(cmd, qty, price, stoploss, takeprofit, cmnt, expr: expr);
                }
            }
            if (ticket >= 0)
            {
                Atrades.Add(mTrade.Id, ticket);
                LogPrint("OpenTrade: new Order created. Id=" + ticket + " cmd=" + cmd.ToString() + " price=" + price + " cmnt=" + cmnt + "count=" + Atrades.Count);
            }
            else
            {
                LogPrint("OpenTrade: OrderSend failed with error #" + GetLastError());
            }
        }

        internal int SendOrder(int cmd, double qty, double price, double stoploss, double takeprofit, string cmnt, datetime expr = null)
        {
            int ticket = -1;
            int count = 10;
            while (ticket < 0 && count > 0)
            {
                count--;
                while (DateTime.Now.Subtract(lastOpen) < TimeSpan.FromSeconds(2.5))
                {
                    Sleep(100);
                }
                ticket = OrderSend(Chart.Symbol, cmd, qty, price, 3, stoploss, takeprofit, cmnt, expiration: expr);
                lastOpen = DateTime.Now;
            }
            return ticket;
        }

        internal void LoadPipValues()
        {
            StreamReader sr = null;
            string path = "C://temp//Pip Value.csv";
            try
            {
                pipValues = new Dictionary<string, double>();
                sr = System.IO.File.OpenText(path);
                string[] seps = { "," };    // commas separted values
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();    // split input line into terms
                    string[] terms = line.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                    if (terms.Length < 2 || terms[0].Length < 7)
                        continue;   // not correct format, get next line
                    double pipValue;
                    if (!double.TryParse(terms[1], out pipValue))  // try to parse pipvalue
                        continue;           // continue with next line if parse double failed.
                    if (pipValue <= 0)
                        continue;           // continue with next line if value is not valid
                    pipValues.Add(terms[0], pipValue);  // Add pipValue to pipValues Dictionalry, key=pair
                }
                LogPrint("LoadPipValues: loaded. count=" + pipValues.Count);
            }
            catch (Exception e)
            {
                LogPrint("LoadPipValues:" + e.Message);
                LogPrint("LoadPipValues:" + e.StackTrace);
                LogPrint("LoadPipValues: path=" + path);
            }
            if (sr != null)     // close streanreader if not null
                sr.Close();
        }

        internal string ConvertFile(string msg)
        {
            string converted = "";
            string pattern = "{lt}";
            string replacement = "<";
            Regex rgx = new Regex(pattern);
            converted = rgx.Replace(msg, replacement);
            pattern = "{gt}";
            replacement = ">";
            rgx = new Regex(pattern);
            converted = rgx.Replace(converted, replacement);
            pattern = "{cr}";
            replacement = "\r";
            rgx = new Regex(pattern);
            converted = rgx.Replace(converted, replacement);
            pattern = "{lf}";
            replacement = "\n";
            rgx = new Regex(pattern);
            converted = rgx.Replace(converted, replacement);
            pattern = "{lb}";
            replacement = "{";
            rgx = new Regex(pattern);
            converted = rgx.Replace(converted, replacement);
            return converted;
        }

        void StartServer(string symbol)
        {
            try
            {
                pipename = "Alveo_" + symbol.Replace("/", "") + "_4";
                LogPrint("pipename=" + pipename);
                server = new NamedPipeServerStream(pipename, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message);
                //TaskInfo ti = new TaskInfo(symbol, server, cts.Token);
                TaskInfo ti = new TaskInfo(symbol, server, this);
                ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadProc), ti);
            }
            catch (IOException e)
            {
                LogPrint(e.Message);
                LogPrint(e.StackTrace);
                throw;
            }
            catch (Exception e)
            {
                LogPrint(e.Message);
                LogPrint(e.StackTrace);
                throw;
            }
            //return _serverTask;
            return;
        }

        public class TaskInfo
        {
            // State information for the task.  These members
            // can be implemented as read-only properties, read/write
            // properties with validation, and so on, as required.
            public NamedPipeServerStream Pipe;
            public string symbol;
            //public CancellationToken Token;
            public Object EA;

            // Public constructor provides an easy way to supply all
            // the information needed for the task.
            //public TaskInfo(string sym, NamedPipeServerStream pipe, CancellationToken token)
            public TaskInfo(string sym, NamedPipeServerStream pipe, Object ea)
            {
                symbol = sym;
                Pipe = pipe;
                //Token = token;
                EA = ea;
            }
        }

        internal void HandleMsg(string msg)
        {
            try
            {
                int count = 0;
                int index = 0;
                bool valid = true;
                Trade trade;
                LogPrint("HandleMsg:");
                while (valid)
                {
                    if (msg.Length > 0)
                    {
                        LogPrint("HandleMsg: msg=" + msg);
                        string converted = ConvertFile(msg);
                        LogPrint("HandleMsg: converted=" + converted);
                        string[] separators = new string[] { "<", ">", "\n", "\r" };
                        string[] elems = converted.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                        count = elems.Length;
                        LogPrint("elems=" + count);
                        if (count > 0)
                        {
                            LogPrint("elems[0]=" + elems[0]);
                            if (elems[0].StartsWith("Message"))
                            {
                                if (elems[1].StartsWith("Body:"))
                                {
                                    string inMsg = elems[1].Replace("Body:", "");
                                    LogPrint("Message=" + inMsg);
                                }
                                break;  // end of message
                            }
                            else if (elems.Length >= 2 && elems[0].StartsWith("Order"))
                            {
                                LogPrint("Order");
                                index = 1;
                                string phrase;
                                while (index < elems.Length - 1)
                                {
                                    trade = new Trade();
                                    while ((phrase = elems[index]) != ("/Order") && index < elems.Length)
                                    {
                                        LogPrint("index=" + index + " phrase=" + phrase);
                                        if (!(valid = phrase.Contains("=")))
                                        {
                                            LogPrint("phrase does not contain colon. phrase=" + phrase);
                                            break;
                                        }
                                        string[] separators2 = new string[] { "=" };
                                        string[] terms = phrase.Split(separators2, StringSplitOptions.RemoveEmptyEntries);
                                        if (!(valid = (terms.Length == 2)))
                                        {
                                            LogPrint("phrase does not contain 2 terms. terns=" + terms.Length + " phrase=" + phrase);
                                            break;
                                        }
                                        int X = int.MinValue;
                                        double D = double.MinValue;
                                        DateTime dt = DateTime.MinValue;
                                        string str = "";
                                        LogPrint("terms=" + terms[0]);
                                        switch (terms[0])
                                        {
                                            case "Oper":
                                                trade.Oper = terms[1].Trim();
                                                break;
                                            case "Symbol":
                                                trade.Symbol = terms[1].Trim();
                                                break;
                                            case "Id":
                                                valid = int.TryParse(terms[1], out X);
                                                trade.Id = X;
                                                trade.IdStr = "MT4," + X.ToString();
                                                break;
                                            case "Side":
                                                str = terms[1].Trim();
                                                trade.Side = ((str == "Buy") ? TradeSide.Buy : ((str == "Sell") ? TradeSide.Sell : TradeSide.Unknown));
                                                break;
                                            case "Type":
                                                str = terms[1].Trim();
                                                trade.Type = (str == "Market") ? TradeType.Market
                                                    : (str == "Limit") ? TradeType.Limit
                                                    : (str == "Stop") ? TradeType.Stop
                                                    : TradeType.Unknown;
                                                break;
                                            case "Quantity":
                                                valid = double.TryParse(terms[1], out D);
                                                trade.Quantity = D;
                                                break;
                                            case "OpenPrice":
                                                valid = double.TryParse(terms[1], out D);
                                                trade.OpenPrice = D;
                                                break;
                                            case "StopLoss":
                                                valid = double.TryParse(terms[1], out D);
                                                trade.StopLoss = D;
                                                break;
                                            case "TakeProfit":
                                                valid = double.TryParse(terms[1], out D);
                                                trade.TakeProfit = D;
                                                break;
                                            case "OpenDate":
                                                if (terms[1].Trim() == "null")
                                                {
                                                    trade.OpenDate = null;
                                                }
                                                else
                                                {
                                                    valid = DateTime.TryParse(terms[1], out dt);
                                                    trade.OpenDate = dt;
                                                }
                                                break;
                                            case "Price":
                                                valid = double.TryParse(terms[1], out D);
                                                trade.Price = D;
                                                break;
                                            case "ClosePrice":
                                                valid = double.TryParse(terms[1], out D);
                                                trade.ClosePrice = D;
                                                break;
                                            case "CloseDate":
                                                if (terms[1].Trim() == "null")
                                                {
                                                    trade.CloseDate = null;
                                                }
                                                else
                                                {
                                                    valid = DateTime.TryParse(terms[1], out dt);
                                                    trade.CloseDate = dt;
                                                }
                                                break;
                                            case "ExpirationDate":
                                                if (terms[1].Trim() == "null")
                                                {
                                                    trade.ExpirationDate = null;
                                                }
                                                else
                                                {
                                                    valid = DateTime.TryParse(terms[1], out dt);
                                                    trade.ExpirationDate = dt;
                                                }
                                                break;
                                            case "Comment":
                                                trade.Comment = terms[1].Trim();
                                                break;
                                            default:
                                                valid = false;
                                                break;
                                        }
                                        if (!valid) break;
                                        index++;
                                    }  // end </Trade>
                                    LogPrint("index=" + index + " valid=" + valid + " phrase=" + elems[index]);
                                    if (!valid) break;
                                    trades.Add(trade.Id, trade);
                                    LogPrint("HandleMsg: Trade added. ID=" + trade.Id + " count=" + trades.Count);
                                    index++;
                                } // end while index
                            }
                            else
                            {
                                valid = false;
                                break;
                            }
                        }
                    }
                    if (trades.Count == 0)
                    {
                        LogPrint("End of message. valid=" + valid + " trades=" + trades.Count);
                    }
                    break;  // end of message
                } // while valid
                if (!valid)
                {
                    LogPrint("Unknowm transmission. index=" + index);
                }
            }
            catch (Exception e)
            {
                LogPrint("Exception HandleMsg: " + e.Message);
                LogPrint("Exception HandleMsg: " + e.StackTrace);
            }
        }


        internal void ThreadProc(Object stateInfo)
        {
            try
            {
                TaskInfo ti = (TaskInfo)stateInfo;
                NamedPipeServerStream pipe = ti.Pipe;
                string symbol = ti.symbol;
                PTask10 ea = (PTask10)ti.EA;
                bool abort;
                //CancellationToken token = ti.Token;
                LogPrint(symbol + " ThreadProc");
                while (!(abort = ea.abortFlag))
                {
                    string msg2 = symbol + " Waiting";
                    LogPrint(msg2);
                    Sleep(20);
                    LogPrint("");
                    pipe.WaitForConnection();
                    abort = ea.abortFlag;
                    LogPrint("ThreadProc: abort=" + abort);
                    if (!abort)
                    {
                        msg2 = "Pipe Connected";
                        LogPrint(msg2);
                        StreamReader sr = new StreamReader(pipe);
                        string msg = sr.ReadToEnd();
                        if (msg.Length > 0)
                        {
                            lock (msgLock)
                            {
                                msgs.Enqueue(msg);
                            }
                        }
                    }
                    pipe.Disconnect();
                }
            }
            catch (Exception e)
            {
                LogPrint(e.Message);
                LogPrint(e.StackTrace);
            }
            LogPrint(" ThreadProc terminating.");
        }

        public enum TradeSide
        {
            Unknown = -1,
            Buy = 0,
            Sell = 1
        }

        public enum TradeType
        {
            Unknown = -1,
            Market = 0,
            Limit = 1,
            Stop = 2
        }

        public class Trade
        {
            public string Oper { get; set; }
            public double CloseAsk { get; set; }
            public double CloseBid { get; set; }
            public DateTime? CloseDate { get; set; }
            public double ClosePrice { get; set; }
            public double ClosePriceHigh { get; set; }
            public string Comment { get; set; }
            public DateTime? ExpirationDate { get; set; }
            public DateTime? FillDate { get; set; }
            public double FilledAsk { get; set; }
            public double FilledBid { get; set; }
            public int Id { get; set; }
            public int ExtId { get; set; }
            public string IdStr { get; set; }
            public int Magic { get; set; }
            public double OpenAsk { get; set; }
            public double OpenBid { get; set; }
            public DateTime? OpenDate { get; set; }
            public double OpenPrice { get; set; }
            public double PendingPrice { get; set; }
            public double Pips { get; set; }
            public double Price { get; set; }
            public double Profit { get; set; }
            public double Quantity { get; set; }
            public TradeSide Side { get; set; }
            public double StopLoss { get; set; }
            public string Symbol { get; set; }
            public double TakeProfit { get; set; }
            public double Commission { get; set; }
            public bool TrailingStop { get; set; }
            public TradeType Type { get; set; }

            public Trade()
            {
                Id = -1;
                IdStr = "";
                Type = TradeType.Unknown;
                Side = TradeSide.Unknown;
                OpenDate = null;
                CloseDate = null;
                Oper = "unknown";
                Commission = 0.00;
            }
        }

        void LogPrint(string msg)
        {
            Print(symbol + ": " + msg);
        }
    }
}