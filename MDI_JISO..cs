/* LEGAL DISCLAIMER  **

The currency markets can do ANYTHING at ANY TIME.

This information was developed by backtesting with 12 months of 2017 historical data of currency market activity.  
No one can guarantee or forecast how these results will perform or behave in future markets.

Anyone who uses this product or this information is responsible for deciding If, Where, When and How this product and information are used.

Anyone who uses this product or this information is responsible and liable for any outcomes that might result from the use of this product or this information.

There is no warranty or guarantee provided or implied for this product or this information for any purpose.

*/

/*
 * Change Log:
 * 
 * Version 1.0  Original.
 * 
 * Version 1.2
 *   1. Cerate c:/temp/MDI_JISO directory if it does not exist, for file storade.
 *   2. Improvements in Save and Restore System EA State.
 *   
 *  Version 1.3
 *   1. Prevent exception when MDI_JISO_State s is null when Start() is called.
 *   
 *  Version 1.4
 *   1. Create directory c:/temp/MDI_JISO.
 *   
 *  Version 1.5
 *   1. Add MaxSpread.
 *   
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using Alveo.Interfaces.UserCode;
using Alveo.Common.Classes;
using Alveo.Common.Enums;
using Alveo.UserCode;

namespace Alveo.UserCode
{
    [Serializable]
    [Description("MDI_JISO Expert Advisor for Alveo")]

    // MDI_JISO Features include:
    // * Expert Advisor for the Alveo trading platform
    // * Unatended Autmated trading 24 hours per day from Sunday evening 22:00:00 UTC to Friday evening UTC 21:55:00
    // * User Specified Trading parameters.
    // * Designed to be run on M1 currency charts.
    // * Uses a McDaniel Dynamic Indiciator (MDI) function to smooth M1 Close Price data.
    // * Uses the slope of MDI to determine market trend direction and Entry point for Buy and Sell orders
    // * Each order uses fixed Stoploss and Take Profit order limits
    // * If enabled, Trailing Stoploss limits are used with all Open orders.
    // * Uses a "Jump In and Scale Out" (JISO) technique to enter the specified number of additional orders at Entry points
    // * Uses JISO to potentually multiply profitability from strong market trend signals.
    // * Evenly distributes JISO Take Profit limits in order to bank profits at regular intervals.
    // * If JISO is used, once an order hits TP limit, the Stoploss limit on all remaining orders is set above BreakEvene level.
    // * Uses JISO to increase ratio of # Winning trades / Total # of Trades ratio
    // * Uses JISO to increase Expectancy, which equals Total Profits / Total # of Trades
    // * Maximum number of simultameous Open trades at any time is JISO + 1.
    // * Optional Trailing Stoploss enable setting
    // * Includes Risk and Money Mangement functionality.
    // * Includes interface functions to simulate Alveo system functions for offline development and testing.
    // * Includes interface functions for using Genetic Algorithm Optimization to seek optimal trading parameter values.
    // * Optimized parameters included for EUR/USD, AUD/USD and GBP/USD currency pairs
    // * Automatically generates a Trade Journal file of all closed trades including OpenPrice, OpenDate, Closed Price and ClosedDate.
    // * Backtested with 12 months of 2017 M1 historical data.
    // * Generates statistics including Profitability, numWins, NumLoss, AvgWin, AvgLoos, MaxDrawdown, numTrades.
    // * Saves all of the EA State data into a restore file every M1 bar and will restore the EA State if Alveo restarts (Init) the EA.
    // * Detailed progress and error messages sent to Alveo Log file.
    // * Include documented Alveo compatible C# source code.
    // * Operation and Design theory are described in an eBook.

    public class MDI_JISO : ExpertAdvisorBase
    {
        datetime datetime0 = 0;
        internal string pair = "";

        #region Properties

        [Category("Settings")]
        [Description("Parameter Set # and Parameter, Journal and TradeLog File to use")]
        public int paramset { get; set; } // specifies dir, dile names and EA strategy name.

        [Category("Settings")]
        [Description("have EA pick optimized parametrs for this Symbol")]
        public bool useOptimzedParams { get; set; }

        [Category("Settings")]
        [Description("Stoploss limit for all trades in Pips. [ex: 37]")]
        public int stoploss { get; set; } // specifies stoploss in Pips

        [Category("Settings")]
        [Description("Takeprofit limit for all trades in Pips. [ex: 41]")]
        public int takeprofit { get; set; } // specifies takeprofit in Pips

        [Category("Settings")]
        [Description("orderQty in standard Lots [ex: 0.02]")]
        public double orderQty { get; set; } // specifies targetQty in Lots

        [Category("Settings")]
        [Description("the scale factor (period) for the MDI Indicator. Larger = more filtering.  [ex: 120]")]
        public double MDIperiod { get; set; }

        [Category("Settings")]
        [Description("specifies the Entry Slope Threshold in multiples of 1e-7.  [ex. 101] ")]
        public int slopeThreshold { get; set; }

        [Category("Settings")]
        [Description("enable Trailing Stop to move StopLoss loss as Price moves towards TakeProfit limit  ")]
        public bool enableTrailigStop { get; set; }

        [Category("Settings")]
        [Description("the number of additional JISO orders created for each Entry point. Range: 0 to 20.  [ex: 4] ")]
        public int JISO { get; set; }

        [Category("Settings")]
        [Description("a Filter value to used to calculate a long-term Average Price, units in percent.  [ex: 0.8] ")]
        public double AvgPriceFilter { get; set; }

        [Category("Settings")]
        [Description("a Price distance from AveragePrice to modify Order Quantity, in Pips  [ex: 12] ")]
        public double farPrice { get; set; }

        [Category("Settings")]
        [Description("Maximum Spread for trade Entry in Pips  [ex: 2.5] ")]
        public double MaxSpread { get; set; }

        #endregion

        public bool simulate;       // simulation mode flag
        public bool optimize;       // optimization mode flag
        public string strategy;     // strategy name;
        string symbol;
        public int simBars;
        public DateTime simTime;
        double pipPos;
        int ticketNum;
        internal double curPrice;
        double simAccountBalance;
        internal bool enableJISOstoplossMod;
        internal MDI_JISO_State s = null;

        DateTime curTime;
        internal BarData curBar;

        string dataFileDir = "C:\\temp\\MDI_JISO\\";
        static System.IO.StreamWriter tradeLogFile;

        internal bool isConnected = true;
        internal bool Stopped = false;
        internal bool isTradeAllowed = false;
        internal bool OKtoTrade = true;
        internal bool timeToExit = false;
        internal bool riskLimitReached = false;
        internal bool tradingclosed = false;

        internal double accountBalance;
        internal double riskLimit = 14;  // in Pips,  i.e. 1.5% of AccountBallance for 1 Standard lot
        internal TimeFrame simTimeframe;

        internal double MDIslope;
        internal double prevMDIslope;

        TimeSpan fridayclose = new TimeSpan(21, 55, 00);  // UTC time
        TimeSpan sundayOpen = new TimeSpan(22, 00, 00);
        TimeSpan dailyMaintStart = new TimeSpan(21, 55, 00);
        TimeSpan dailyMaintEnd = new TimeSpan(22, 30, 00);

        int nStep = 0;  // used to find potential Exception location in EA constructor
        bool spreadTooHigh;

        // EA constructor
        public MDI_JISO()
        {
            // Basic EA initialization. Don't use this constructor to calculate values
            pair = "";
            copyright = "";
            link = "";
            simulate = false;
            optimize = simulate;
            nStep = 1;
            InitEA(1);
        }

        // EA constuctor with parameters
        // used by Test Program
        public MDI_JISO(string thePair, bool optimizing)
        {
            // Basic EA initialization. Don't use this constructor to calculate values
            pair = thePair;
            copyright = "";
            link = "";
            simulate = true;
            optimize = optimizing;
            nStep = 2;
            InitEA(0, thePair);
        }

        // Common EA constructor initialization
        // DO NOT include any calls to Alveo here
        public void InitEA(int set, string thePair = "EUR/USD")
        {
            try
            {
                // Initializa EA variables
                nStep = 3;
                spreadTooHigh = false;
                useOptimzedParams = (optimize) ? false : true;
                strategy = "MDI_JISO";
                AvgPriceFilter = 0.8;
                farPrice = 0;
                MaxSpread = 2.0;    // Pips
                JISO = 4;
                stoploss = 41;
                takeprofit = 45;
                MDIperiod = 128;
                slopeThreshold = 73;  // * 1e-7
                paramset = set;
                simBars = 0;
                ticketNum = 0;
                orderQty = 0.02;
                MDIslope = 0;
                prevMDIslope = 0;
                nStep = 4;
                curPrice = double.MinValue;
                simTime = DateTime.UtcNow;
                simTimeframe = TimeFrame.M1;
                curTime = simTime;
                curBar = null;
                riskLimitReached = false;
                dataFileDir = "C:\\temp\\MDI_JISO\\";
                isConnected = true;
                Stopped = false;
                isTradeAllowed = false;
                OKtoTrade = true;
                timeToExit = false;
                enableTrailigStop = false;
                enableJISOstoplossMod = false;
                simAccountBalance = 1000.00;
                nStep = 98;
            }
            catch (Exception e)
            {
                nStep = 99;
                Print(e.Message);  // Prints dont work this early in Alveo
                Print(e.StackTrace);
                Sleep(500);
            }
            return;
        }

        // Write msg to Trade Log file
        // optionally Clear Trade Log file before writing msg
        internal void WriteTradeLog(string msg, bool clear = false)
        {
            // Write msg to TradeLog, Delete and Create new file is clear=true
            string tradeLogFilename = "TradeLog" + symbol.Replace("/", "") + paramset + ".csv";
            if (!System.IO.Directory.Exists(dataFileDir))
                System.IO.Directory.CreateDirectory(dataFileDir);
            if (clear)
                System.IO.File.Delete(dataFileDir + tradeLogFilename);
            tradeLogFile = new System.IO.StreamWriter(dataFileDir + tradeLogFilename, true); // create new file
            tradeLogFile.WriteLine(msg);
            tradeLogFile.Close();
        }

        //external interface for Simulator
        internal int doInit()
        {
            return this.Init();
        }


        //+------------------------------------------------------------------+"
        //| EA initialization function                                       |"
        //|                                                                  |"
        //| called by Alveo to init EA                                       |"
        //+------------------------------------------------------------------+"
        protected override int Init()
        {
            try
            {
                //Print("nStep=" + nStep);  // if needed.
                symbol = GetSymbol();
                if (!simulate)  // if running in Alveo
                {
                    pair = Chart.Symbol;
                }
                if (pair == "")
                    pair = "EURUSD";  // default
                symbol = pair;
                if (s == null)
                    s = new MDI_JISO_State(paramset, symbol);  // s = new State object
                pipPos = symbol.EndsWith("JPY") ? 100 : 10000;  // determine Pip position
                if (!optimize)
                    LogPrint("Version = " + MDI_JISO_State.VERSION);

                if (s == null)
                    throw new Exception("Init: s == null");
                var restored = s.CheckRestore();  // check if State Restore is needed
                if (s.firstRun)
                {
                    if (!optimize)
                    {
                        if (!System.IO.Directory.Exists("C:\\temp"))
                            System.IO.Directory.CreateDirectory("C:\\temp");
                        if (!System.IO.Directory.Exists(dataFileDir))
                            System.IO.Directory.CreateDirectory(dataFileDir);
                        WriteTradeLog("Type,ID,Qty,OpenDate,OpenPice,Stoploss,TakeProfit,MDI,Slope,CloseDate,ClosePrice,dProfit,Balance", true);
                    }
                    s.firstRun = false;
                }
                if (useOptimzedParams)  // override User Settings with optimized values
                {
                    if (symbol == "EUR/USD")
                    {
                        JISO = 4;
                        stoploss = 41;
                        takeprofit = 45;
                        MDIperiod = 128;
                        slopeThreshold = 73;  // * 1e-7
                    }
                    else if (symbol == "AUD/USD")
                    {
                        JISO = 4;
                        stoploss = 58;
                        takeprofit = 62;
                        MDIperiod = 45;
                        slopeThreshold = 371;
                    }
                    else if (symbol == "GBP/USD")
                    {
                        JISO = 4;
                        stoploss = 63;
                        takeprofit = 67;
                        MDIperiod = 48;
                        slopeThreshold = 522; //* 1e-7
                    }
                    else
                    {
                        JournalLog(" Do not have optimized parameters for Symbol " + symbol + ". Using EUR-USD parameters.");
                        JISO = 4;
                        stoploss = 41;
                        takeprofit = 45;
                        MDIperiod = 128;
                        slopeThreshold = 73;
                    }
                }
                if (!optimize && restored)
                    JournalLog(" Init: EA State was Restored.");
                if (optimize)
                    LoadParameters();  // load trading parameters from file for optimization
                simTime = DateTime.UtcNow;
                s.nBars = 0;
                s.curBars = 0;
                s.closedOrders.Clear();  // clear closedOrders list
                if (!optimize)
                    LogPrint(" Init: simulate=" + simulate.ToString());
            }
            catch (Exception e)  // Exception Handling
            {
                Print(e.Message);
                Print(e.StackTrace);
                Sleep(1000);
            }
            return 0;
        }

        //+------------------------------------------------------------------+"
        //| expert deinitialization function                                 |"
        //|                                                                  |"
        //| called by Alveo to unitialize EA                                 |"
        //+------------------------------------------------------------------+"
        protected override int Deinit()
        {
            LogPrint(" Deinit: Shutting Down.");
            Sleep(1000);
            if (s != null)
                s.CloseState();
            return 0;
        }

        // eternal interface for Simulator
        internal int doStart()
        {
            return this.Start();
        }

        //+------------------------------------------------------------------+"
        //| expert start function                                            |"
        //|                                                                  |"
        //| called by Alveo for every new bar on chart                       |"
        //| and maybe more often than that.                                  |"
        //+------------------------------------------------------------------+"
        protected override int Start()
        {
            try
            {
                symbol = GetSymbol();
                if (s == null)
                    s = new MDI_JISO_State(paramset, symbol);  // s = new State object
                accountBalance = GetAccoutBalance();
                //orderQty = Math.Max(Math.Round((Math.Sqrt(Math.Max(accountBalance, 0)) - 31) / 10), 1) * 0.01;
                if (!simulate)
                {
                    if (DetectChanged(ref s.OKtoTrade, CheckOKToTrade()))
                    {
                        JournalLog(" OKtoTrade=" + s.OKtoTrade.ToString());
                    }
                    if (!s.OKtoTrade)
                    {
                        JournalLog(" OKtoTrade=" + s.OKtoTrade.ToString());
                        Sleep(1000);
                        return 0; // EA cannot trade at this time
                    }
                }
                var bars = GetBars();
                if (bars == 0)  // no chart bars
                {
                    Sleep(1000);    // sleep 1 sec
                    return 0;
                }
                if (s.curBars == bars)  // if #bar is unchanged
                {
                    Sleep(1000);    // sleep 1 sec
                    return (0);  // nothing to do until new bar arrives
                }
                s.curBars = bars;                 // update curBars
                s.nBars++;                      // count bars
                s.dI = GetCurBar();             // save bar data
                curTime = s.dI.BarTime;
                Monitor();                      // perform Monitor functions
                if (!optimize && !simulate)
                    s.SaveSystemState();        // save EA State every new Bar
            }
            catch (Exception e)     // Exception handling
            {
                LogPrint(strategy + " Start: Exception: " + e.Message);
                LogPrint("Exception: " + e.InnerException);
                LogPrint("Exception: " + e.StackTrace);
                Sleep(500);
            }
            return 0;
        }

        // get number of Bar on chart
        internal int GetBars()
        {
            if (simulate)
                return simBars;
            else  // Alveo function
                return Bars;
        }

        // Mponitor for Closed orders
        // Monitor for Start of Day and start of Hour
        // Simulate market Stoploss and TakeProfit
        // Update MDI value, monitor MDIslope and create new Orders
        public void Monitor()
        {
            int total = GetTotalOrders();
            // count trading numDays
            if (s.OldTime3 > curTime || curTime >= s.nextDay)  // start of new Day
            {
                s.stats.numDays++;
                s.OldTime3 = curTime;
                s.nextDay = new DateTime(
                  s.OldTime3.Year,
                  s.OldTime3.Month,
                  s.OldTime3.Day,
                  0, 0, 0, 0) + TimeSpan.FromHours(24);
                s.startingBalance = accountBalance;  // save Daily startingBalance
            }
            // count trading numHours
            if (s.OldTime2 > curTime || curTime.Subtract(s.OldTime2) >= TimeSpan.FromHours(1))  // start of new Hour
            {
                s.stats.numHours++;
                s.OldTime2 = curTime;
                s.ordersThisHour = 0;
                if (!optimize)
                    JournalLog(" Monitor still running. " + curTime.ToLongTimeString());
            }

            curPrice = s.dI.close;

            if (AvgPriceFilter > 0)  // calculate long-term averagerPrice
            {
                var alpha = Math.Abs(AvgPriceFilter / 100);
                if (s.averagerPrice != double.MinValue)
                    s.averagerPrice += alpha * (curPrice - s.averagerPrice);
                else
                    s.averagerPrice = curPrice;
            }
            else  // disabled
                s.averagerPrice = 0;

            if (s.dIprev == null) // if no previous bar
            {
                s.dI.MDI = curPrice;  // save s.dIprev data
                s.dIprev = s.dI;
                prevMDIslope = 0;
                return;  // nothing to do without previous bar
            }
            else  //  have s.dI and s.dIprev 
            {
                // Calculate MDi and MDIslope
                // newMD = prevMD + (Price– prevMD) / (N * Power((Price / prevMD) , 4))  where N is similar to the period of a MA.
                if (s.dIprev.MDI <= 0)
                    s.dIprev.MDI = (double)(s.dIprev.open + s.dIprev.high + s.dIprev.low + s.dIprev.close) / 4.0;
                var prevMDI = s.dIprev.MDI;
                s.dIprev = s.dI; // save previous bar
                //var thePrice = curPrice;
                s.dI.typical = (double)(s.dI.open + s.dI.high + s.dI.low + s.dI.close) / 4.0;
                var thePrice = s.dI.typical; // DFB
                var newMDI = prevMDI + (thePrice - prevMDI) / (MDIperiod * Math.Pow(Math.Abs(thePrice / prevMDI), 4.0));
                s.dI.MDI = newMDI;
                MDIslope = 2 * prevMDIslope / 3 + (newMDI - prevMDI) / 3;
                prevMDIslope = MDIslope;

                // wait for MDI to stabilize
                if ((double)s.nBars < MDIperiod / 2)
                    return;
                double ask = GetMarketInfo(symbol, MODE_ASK);
                double bid = GetMarketInfo(symbol, MODE_BID);
                var spread = ask - bid;
                if (MaxSpread > 0 && spread > MaxSpread)
                {
                    if (!optimize && !spreadTooHigh)
                        LogPrint(strategy + " Monitor: Spread too high. Spread=" + spread);
                    spreadTooHigh = true;
                    return;
                }
                else
                    spreadTooHigh = false;
                if (total == 0 && Math.Abs(MDIslope) > ((double)slopeThreshold) * 1e-7) // No Open orders and MDIslope > threshold
                {
                    s.targetDir = (MDIslope > 0) ? 1 : -1;  // det direction based upon MDIslope
                    string msg = newMDI.ToString("F5") + "," + MDIslope.ToString("F7");
                    CreateOrder(msg);  // create first open order
                }
                if (s.nBars % 5 == 0)  // send message every 5 minutes
                {
                    if (!optimize)
                    {
                        var pct = MDIslope * 1e7 / ((double)slopeThreshold);
                        JournalLog(": Slope%=" + pct.ToString("F2") + " targetDir=" + s.targetDir);
                    }
                }
            }

            RemoveClosedOrders();   // clean up lists of orders

            if (simulate)  // remove Orders that exceeded StopLoss or TakeProfit limits
            {
                var highest = s.dI.high;
                var lowest = s.dI.low;

                // check buyOpenOrders for TP or SL
                if (s.buyOpenOrders.Count > 0)
                {
                    foreach (var order in s.buyOpenOrders.Values)
                    {
                        double tp = (double)order.TakeProfit;
                        double sl = (double)order.StopLoss;
                        if (highest >= tp)
                        {
                            ExitOpenTrade(reason: 1, order: order, price: tp + 2 / pipPos);
                        }
                        else if (lowest <= sl)
                        {
                            ExitOpenTrade(reason: 2, order: order, price: sl - 2 / pipPos);
                        }
                    }
                }
                // check sellOpenOrders for TP or SL
                if (s.sellOpenOrders.Count > 0)
                {
                    foreach (var order in s.sellOpenOrders.Values)
                    {
                        double tp = (double)order.TakeProfit;
                        double sl = (double)order.StopLoss;
                        if (lowest <= tp)
                        {
                            ExitOpenTrade(reason: 3, order: order, price: tp - 2 / pipPos);
                        }
                        else if (highest >= sl)
                        {
                            ExitOpenTrade(reason: 4, order: order, price: sl + 2 / pipPos);
                        }
                    }
                }
            }

            // find Order closed by TP
            // update Trailing Stoploss
            Order foundOrder = null;
            if (s.buyOpenOrders.Count > 0)
            {
                foreach (var order in s.buyOpenOrders.Values)
                {
                    int ticketID = (int)order.Id;
                    var tm = GetOrderCloseTime(ticketID);
                    if (tm.DateTime.Year > 1970) // Closed
                    {
                        var dProfit = TradeClosed(order);
                        if (!optimize)
                            LogPrint("FT: Monitor: Closed Buy order. ID=" + order.Id
                                + " CloseTime=" + tm.ToString());
                        if (foundOrder == null)
                        {
                            //if (priceDiff >= takeprofit / pipPos) // was TakeProfit
                            if (order.ClosePrice >= order.TakeProfit) // was TakeProfit
                            {
                                if (!optimize)
                                    LogPrint("was TakeProfit");
                                //targetDir = 1;
                                foundOrder = order;
                            }
                            else if (order.ClosePrice <= order.StopLoss) // Stoploss
                            {
                                if (!optimize)
                                    LogPrint("was Stoploss");
                                //targetDir = -1;
                            }
                        }
                    }
                    else // !closed
                    {
                        // update Trailing Stoploss
                        if (enableTrailigStop && curPrice - (double)order.StopLoss - stoploss / pipPos > 4 / pipPos) // if curPrice moved more than 4 Pip
                        {
                            order.StopLoss = (decimal)(curPrice - (double)stoploss / pipPos);
                            ModifyOrder(order, (double)order.StopLoss);
                            LogPrint("Monitor: Order=" + order.Id + " side=" + order.Side + " Trailing Stoploss=" + order.StopLoss.ToString("F5"));
                        }
                    }
                }
            }
            if (foundOrder == null)
            {
                // check closed sellOpenOrders for TP or SL and set targetDir
                if (s.sellOpenOrders.Count > 0)
                {
                    foreach (var order in s.sellOpenOrders.Values)
                    {
                        int ticketID = (int)order.Id;
                        var tm = GetOrderCloseTime(ticketID);
                        if (tm.DateTime.Year > 1970) // Closed
                        {
                            var dProfit = TradeClosed(order);
                            if (!optimize)
                                LogPrint(strategy + "Monitor: Closed Sell order. ID=" + order.Id
                                + " CloseTime=" + tm.ToString());
                            if (foundOrder == null)
                            {
                                if (order.ClosePrice >= order.StopLoss) // was StopLoss
                                {
                                    if (!optimize)
                                        LogPrint("was Stoploss");
                                    //targetDir = 1;
                                }
                                else if (order.ClosePrice <= order.TakeProfit)  // TakeProfit
                                {
                                    if (!optimize)
                                        LogPrint("was TakeProfit");
                                    //targetDir = -1;
                                    foundOrder = order;
                                }
                            }
                        }
                        else // !closed, update trailing Stoploss
                        {
                            if (enableTrailigStop && (double)order.StopLoss - curPrice - stoploss / pipPos > 4 / pipPos)
                            {
                                order.StopLoss = (decimal)(curPrice + (double)stoploss / pipPos);
                                ModifyOrder(order, (double)order.StopLoss);
                                LogPrint("Monitor: Order=" + order.Id + " side=" + order.Side + " Trailing Stoploss=" + order.StopLoss.ToString("F5"));
                            }
                        }
                    }
                }
            }

            RemoveClosedOrders(); // clean up closed orders

            // if enableJISOstoplossMod and JISO AND TP triggered, then move Stoploss to BE
            if (foundOrder != null)  // found TP
            {
                if (!optimize)
                    LogPrint("foundOrder=" + foundOrder.Id);
                if (enableJISOstoplossMod && JISO > 0)
                {
                    total = GetTotalOrders();
                    if (total > 0)
                    {
                        decimal delta = (decimal)(2.0 / pipPos);
                        foreach (var order in s.buyOpenOrders.Values)
                        {
                            if (order.StopLoss < order.OpenPrice) // move StopLoss to above Breakeven level
                                if (curPrice - (double)order.OpenPrice >= 9.0 / pipPos)
                                {
                                    //modify order StopLoss
                                    var orderSL = order.OpenPrice + delta;
                                    order.StopLoss = (orderSL > order.StopLoss) ? orderSL : order.StopLoss;
                                    ModifyOrder(order, (double)order.StopLoss);
                                }
                        }
                        foreach (var order in s.sellOpenOrders.Values)
                        {
                            if (order.StopLoss > order.OpenPrice) // move StopLoss to above Breakeven level
                                if ((double)order.OpenPrice - curPrice >= 9.0 / pipPos)
                                {
                                    //modify order StopLoss
                                    var orderSL = order.OpenPrice - delta;
                                    order.StopLoss = (orderSL < order.StopLoss) ? orderSL : order.StopLoss;
                                    ModifyOrder(order, (double)order.StopLoss);
                                }
                        }
                    }
                }
                foundOrder = null;
            }
        }

        // remove closedOrders from buyOpenOrders and sellOpenOrders
        internal void RemoveClosedOrders()
        {
            if (s.closedOrders.Count > 0)
                foreach (var iD in s.closedOrders.Keys)
                {
                    s.buyOpenOrders.Remove(iD);
                    s.sellOpenOrders.Remove(iD);
                }
            s.closedOrders.Clear();
        }


        // EA State object to remeber data that must be Restored after distrubtion and Init
        [Serializable]
        internal class MDI_JISO_State
        {
            internal const string VERSION = "1.4";  //Restore file format
            internal bool firstRun = true;
            internal bool isConnected = true;
            internal bool Stopped = false;
            internal bool isTradeAllowed = false;
            internal bool OKtoTrade = true;
            internal bool timeToExit = false;
            internal int curBars;
            internal Statistics stats = new Statistics(0);
            internal BarData dI;
            internal BarData dIprev;
            internal int nBars = 0;
            internal DateTime OldTime, OldTime2, OldTime3, nextDay;
            internal int targetDir;
            internal double startingBalance;
            internal int ordersThisHour;
            System.IO.StreamWriter restore = null;
            System.IO.StreamReader restoreRead = null;
            internal string restorePath;
            private Object thisLock = new Object();
            string dataFileDir = "C:\\temp\\MDI_JISO\\";
            int paramset = 1;
            internal string stateSymbol;
            internal double averagerPrice;
            internal Dictionary<long, Order> buyOpenOrders = null;
            internal Dictionary<long, Order> sellOpenOrders = null;
            internal Dictionary<long, Order> closedOrders = null;


            /// <summary>
            /// DENA3_State Class object constructor
            /// </summary>
            public MDI_JISO_State()
            {
                paramset = 1;
                stateSymbol = "EUR/USD";
                ClearState();
            }

            // Constructor with parameters
            public MDI_JISO_State(int set, string chartSymbol)
            {
                paramset = set;
                stateSymbol = chartSymbol;
                ClearState();
            }

            /// <summary>
            /// DENA3_State initialization
            /// </summary>
            internal void ClearState()
            {
                if (firstRun)
                {
                    lock (thisLock)  // lock to prevent multiple access of file
                    {
                        restorePath = dataFileDir + "Restore" + stateSymbol.Replace('/', '-') + paramset + ".csv";
                        System.IO.File.Delete(restorePath);  // Delete Restore file on starteup
                    }
                }

                curBars = 0;
                nBars = 0;
                isConnected = true;
                Stopped = false;
                isTradeAllowed = false;
                timeToExit = false;
                stats = new Statistics(0);
                dI = null;
                dIprev = null;
                targetDir = 0;
                startingBalance = double.MinValue;
                ordersThisHour = 0;
                OldTime = DateTime.UtcNow;
                OldTime2 = OldTime;
                OldTime3 = DateTime.UtcNow;
                nextDay = new DateTime(
                      OldTime3.Year,
                      OldTime3.Month,
                      OldTime3.Day,
                      0, 0, 0, 0) + TimeSpan.FromHours(24);
                buyOpenOrders = new Dictionary<long, Order>();
                sellOpenOrders = new Dictionary<long, Order>();
                closedOrders = new Dictionary<long, Order>();
                averagerPrice = double.MinValue;
            }

            // Delete Resore file on Shutdown
            internal void CloseState()
            {
                lock (thisLock)
                {
                    if (restore != null)
                    {
                        restore.Close();
                        restore = null;
                    }
                    if (restoreRead != null)
                    {
                        restoreRead.Close();
                        restoreRead = null;
                    }
                    restorePath = dataFileDir + "Restore" + stateSymbol.Replace('/', '-') + paramset + ".csv";
                    System.IO.File.Delete(restorePath);
                }
            }

            // Save EA State
            public void SaveSystemState()
            {
                lock (thisLock)
                {
                    restore = new System.IO.StreamWriter(restorePath, false);
                    SaveState();
                    SaveAllOrders(ref restore);
                    restore.Close();
                    restore = null;
                }
                //JournalLog("SaveState successful.");
            }

            /// <summary>
            /// Save All Orders in orders list to restore file
            /// <param name="restore">ref to System.IO.StreamWriter</param>
            /// </summary>
            internal void SaveAllOrders(ref System.IO.StreamWriter restore)
            {
                restore.WriteLine(buyOpenOrders.Count);
                foreach (var kvp in buyOpenOrders)
                {
                    restore.WriteLine(kvp.Key);
                    SaveOrder(ref restore, kvp.Value);
                }
                restore.WriteLine(sellOpenOrders.Count);
                foreach (var kvp in sellOpenOrders)
                {
                    restore.WriteLine(kvp.Key);
                    SaveOrder(ref restore, kvp.Value);
                }
            }

            /// <summary>
            /// Save clsOrder object to restore file
            /// <param name="restore">ref to System.IO.StreamWriter</param>
            /// </summary>
            public void SaveOrder(ref System.IO.StreamWriter restore, Order order)
            {
                order.Symbol = stateSymbol;
                restore.WriteLine(order.Id + ", " + order.Symbol);
                restore.WriteLine(order.OpenDate + ", " + order.Side);
                restore.WriteLine(order.Comment);
                restore.WriteLine(order.OpenPrice
                + ", " + order.ClosePrice
                + ", " + order.Profit
                + ", " + order.TakeProfit
                + ", " + order.OpenBid
                + ", " + order.OpenAsk);
                restore.WriteLine(order.TrailingStop
                + ", " + order.CloseDate
                + ", " + order.Price
                + ", " + order.Type
                + ", " + order.Quantity
                + ", " + order.StopLoss);
                restore.WriteLine(order.FilledBid
                + ", " + order.FilledAsk
                + ", " + ((order.FillDate == null) ? DateTime.MinValue : order.FillDate).ToString()
                );
                restore.WriteLine(order.CustomId);
                restore.WriteLine(order.ExternId
                + ", " + order.Status
                + ", " + order.TimeInForce
                + ", " + ((order.ExpirationDate == null) ? DateTime.MinValue : order.ExpirationDate).ToString()
                );
            }


            /// <summary>
            /// Save DENA3_State to restore file
            /// <param name="restore">ref to System.IO.StreamWriter</param>
            /// </summary>
            public void SaveState()
            {
                restore.WriteLine(VERSION + ", " + stateSymbol);
                restore.WriteLine(DateTime.UtcNow.ToString());
                restore.WriteLine(firstRun.ToString()
                    + ", " + isConnected.ToString()
                    + ", " + Stopped.ToString()
                    + ", " + isTradeAllowed.ToString()
                    + ", " + OKtoTrade.ToString()
                    + ", " + timeToExit.ToString()
                    );
                restore.WriteLine(curBars
                    + ", " + nBars
                    + ", " + targetDir
                    + ", " + startingBalance
                    + ", " + ordersThisHour
                    + ", " + averagerPrice
                    );
                restore.WriteLine(OldTime
                    + ", " + OldTime2
                    + ", " + OldTime3
                    + ", " + nextDay
                    );
                stats.SaveStats(ref restore);
                if (dI == null)
                    restore.WriteLine("null");
                else
                    dI.SaveBarData(ref restore);
                if (dIprev == null)
                    restore.WriteLine("null");
                else
                    dIprev.SaveBarData(ref restore);
            }

            // return True if Resore file exists
            internal bool CheckRestore()
            {
                bool ret = false;
                if (System.IO.File.Exists(restorePath))
                    ret = RestoreSystemState();
                return ret;
            }

            // Restore EA State from file
            internal bool RestoreSystemState()
            {
                bool restored = false;
                lock (thisLock)  // lock to prevent multiple access to file
                {
                    restoreRead = new System.IO.StreamReader(restorePath);
                    RestoreState(ref restoreRead);
                    RestoreAllOrders(ref restoreRead);
                    restoreRead.Close();
                    restoreRead = null;
                    restored = true;
                }
                return restored;
            }

            // Restore Lists of orders from file
            internal void RestoreAllOrders(ref System.IO.StreamReader restore)
            {
                char[] delimiterChars = { ',', '\t' };
                String line;
                string[] vals;
                string parseError = "Exception RestoreState: parse error at ";
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 1)
                    throw new Exception(parseError + "buyOpenOrders.Count");
                int cnt;
                int key;
                if (!int.TryParse(vals[0], out cnt))
                    throw new Exception(parseError + "buyOpenOrders.Count");
                buyOpenOrders.Clear();
                if (cnt > 0)
                    for (int i = 0; i < cnt; i++)
                    {
                        if (!int.TryParse(vals[0], out key))
                            throw new Exception(parseError + "key");
                        var order = RestoreOrder(ref restore);
                        buyOpenOrders.Add(key, order);
                    }
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 1)
                    throw new Exception(parseError + "sellOpenOrders.Count");
                if (!int.TryParse(vals[0], out cnt))
                    throw new Exception(parseError + "sellOpenOrders.Count");
                sellOpenOrders.Clear();
                if (cnt > 0)
                    for (int i = 0; i < cnt; i++)
                    {
                        line = restore.ReadLine();
                        vals = line.Split(delimiterChars);
                        if (vals.Length != 1)
                            throw new Exception(parseError + "key");
                        if (!int.TryParse(vals[0], out key))
                            throw new Exception(parseError + "key");
                        var order = RestoreOrder(ref restore);
                        sellOpenOrders.Add(key, order);
                    }
                return;
            }

            // Restore Order data from file
            public Order RestoreOrder(ref System.IO.StreamReader restore)
            {
                Order order = new Order();
                char[] delimiterChars = { ',', '\t' };
                String line;
                string[] vals;
                string parseError = "Exception RestoreState: parse error at ";
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                long key;
                DateTime dt;
                TradeSide side;
                TradeType type;
                decimal dcml;
                bool tf;
                OrderStatus stat;
                TimeInForce tif;
                if (vals.Length != 2)
                    throw new Exception(parseError + "order.Id");
                if (!long.TryParse(vals[0], out key))
                    throw new Exception(parseError + "order.Id");
                order.Id = key;
                order.Symbol = vals[1].Trim();
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 2)
                    throw new Exception(parseError + "OpenDate");
                if (!DateTime.TryParse(vals[0], out dt))
                    throw new Exception(parseError + "OpenDate");
                order.OpenDate = dt;
                if (!TradeSide.TryParse(vals[1], out side))
                    throw new Exception(parseError + "order.Id");
                order.Side = side;
                line = restore.ReadLine();
                order.Comment = line.Trim();
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 6)
                    throw new Exception(parseError + "OpenPrice");
                if (!Decimal.TryParse(vals[0], out dcml))
                    throw new Exception(parseError + "order.OpenPrice");
                order.OpenPrice = dcml;
                if (!Decimal.TryParse(vals[1], out dcml))
                    throw new Exception(parseError + "order.ClosePrice");
                order.ClosePrice = dcml;
                if (!Decimal.TryParse(vals[2], out dcml))
                    throw new Exception(parseError + "order.Profit");
                order.Profit = dcml;
                if (!Decimal.TryParse(vals[3], out dcml))
                    throw new Exception(parseError + "order.TakeProfit");
                order.TakeProfit = dcml;
                if (!Decimal.TryParse(vals[4], out dcml))
                    throw new Exception(parseError + "order.OpenBid");
                order.OpenBid = dcml;
                if (!Decimal.TryParse(vals[5], out dcml))
                    throw new Exception(parseError + "order.OpenAsk");
                order.OpenAsk = dcml;
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 6)
                    throw new Exception(parseError + "TrailingStop");
                if (!bool.TryParse(vals[0], out tf))
                    throw new Exception(parseError + "order.TrailingStop");
                order.TrailingStop = tf;
                if (!DateTime.TryParse(vals[1], out dt))
                    throw new Exception(parseError + "order.TrailingStop");
                order.CloseDate = dt;
                if (!Decimal.TryParse(vals[2], out dcml))
                    throw new Exception(parseError + "order.Price");
                order.Price = dcml;
                if (!TradeType.TryParse(vals[3], out type))
                    throw new Exception(parseError + "order.Type");
                order.Type = type;
                if (!Decimal.TryParse(vals[4], out dcml))
                    throw new Exception(parseError + "order.Quantity");
                order.Quantity = dcml;
                if (!Decimal.TryParse(vals[5], out dcml))
                    throw new Exception(parseError + "order.StopLoss");
                order.StopLoss = dcml;
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 3)
                    throw new Exception(parseError + "FilledBid");
                if (!decimal.TryParse(vals[0], out dcml))
                    throw new Exception(parseError + "order.FilledBid");
                order.FilledBid = dcml;
                if (!decimal.TryParse(vals[1], out dcml))
                    throw new Exception(parseError + "order.FilledAsk");
                order.FilledAsk = dcml;
                if (!DateTime.TryParse(vals[2], out dt))
                    throw new Exception(parseError + "order.FillDate");
                order.FillDate = dt;
                line = restore.ReadLine();
                order.CustomId = line.Trim();
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 4)
                    throw new Exception(parseError + "ExternId");
                if (!long.TryParse(vals[0], out key))
                    throw new Exception(parseError + "order.ExternId");
                order.ExternId = key;
                if (!OrderStatus.TryParse(vals[1], out stat))
                    throw new Exception(parseError + "order.Status");
                order.Status = stat;
                if (!TimeInForce.TryParse(vals[2], out tif))
                    throw new Exception(parseError + "order.Status");
                order.TimeInForce = tif;
                if (!DateTime.TryParse(vals[3], out dt))
                    throw new Exception(parseError + "order.ExpirationDate");
                order.ExpirationDate = dt;
                return order;
            }

            // Resore EA State from file
            public void RestoreState(ref System.IO.StreamReader restore)
            {
                char[] delimiterChars = { ',', '\t' };
                String line;
                string[] vals;
                string parseError = "Exception RestoreState: parse error at ";
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 2)
                    throw new Exception(parseError + "Version");
                if (vals[1].Trim() != stateSymbol)
                    throw new Exception(parseError + "Symbol. Incorrect.");
                line = restore.ReadLine();
                DateTime dt;
                if (!DateTime.TryParse(line, out dt))
                    throw new Exception(parseError + "DateTime");
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 6)
                    throw new Exception(parseError + "firstRun");
                if (!bool.TryParse(vals[0], out firstRun))
                    throw new Exception(parseError + "firstRun");
                if (!bool.TryParse(vals[1], out isConnected))
                    throw new Exception(parseError + "isConnected");
                if (!bool.TryParse(vals[2], out Stopped))
                    throw new Exception(parseError + "Stopped");
                if (!bool.TryParse(vals[3], out isTradeAllowed))
                    throw new Exception(parseError + "isTradeAllowed");
                if (!bool.TryParse(vals[4], out OKtoTrade))
                    throw new Exception(parseError + "OKtoTrade");
                if (!bool.TryParse(vals[5], out timeToExit))
                    throw new Exception(parseError + "timeToExit");
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 6)
                    throw new Exception(parseError + "curBars");
                if (!int.TryParse(vals[0], out curBars))
                    throw new Exception(parseError + "curBars");
                if (!int.TryParse(vals[1], out nBars))
                    throw new Exception(parseError + "nBars");
                if (!int.TryParse(vals[2], out targetDir))
                    throw new Exception(parseError + "targetDir");
                if (!double.TryParse(vals[3], out startingBalance))
                    throw new Exception(parseError + "startingBalance");
                if (!int.TryParse(vals[4], out ordersThisHour))
                    throw new Exception(parseError + "ordersThisHour");
                if (!double.TryParse(vals[5], out averagerPrice))
                    throw new Exception(parseError + "averagerPrice");
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 4)
                    throw new Exception(parseError + "OldTime");
                if (!DateTime.TryParse(vals[0], out OldTime))
                    throw new Exception(parseError + "OldTime");
                if (!DateTime.TryParse(vals[1], out OldTime2))
                    throw new Exception(parseError + "OldTime2");
                if (!DateTime.TryParse(vals[2], out OldTime3))
                    throw new Exception(parseError + "OldTime3");
                if (!DateTime.TryParse(vals[3], out nextDay))
                    throw new Exception(parseError + "nextDay");
                stats.RestoreStats(ref restore);
                line = restore.ReadLine().Trim();
                if (line != "null")
                    dI.RestoreBarData(ref restore, line);
                else
                    dI = null;
                line = restore.ReadLine().Trim();
                if (line != "null")
                    dI.RestoreBarData(ref restore, line);
                else
                    dIprev = null;
            }
        }

        // Statistics on EA operation
        public struct Statistics
        {
            internal double profitLoss;
            internal double maxProfit;
            internal double maxLoss;
            internal double maxDrawdown;
            internal int numContracts;
            internal int numWins;
            internal int numLoss;
            internal double avgWin;
            internal double avgLoss;
            internal double winRate;
            internal double expectancy;
            internal int numOpen;
            internal int numDays;
            internal int numHours;
            internal int numTrades;
            internal int orderExits;
            internal int computeStats;

            /// <summary>
            /// Initialize Trading Statistics
            /// </summary>
            internal Statistics(int x)
            {
                profitLoss = 0;
                maxProfit = 0;
                maxLoss = 0;
                maxDrawdown = 0;
                numContracts = 0;
                numWins = 0;
                numLoss = 0;
                avgWin = 0;
                avgLoss = 0;
                winRate = 0;
                expectancy = 0;
                numOpen = 0;
                numDays = 0;
                numHours = 0;
                numTrades = 0;
                orderExits = 0;
                computeStats = 0;
            }

            /// <summary>
            /// SaveStats: saves Trading Statistics to "restore" file.
            /// <param name="restore">ref to System.IO.StreamWriter</param>
            /// </summary>
            /// <returns>void</returns>
            internal void SaveStats(ref System.IO.StreamWriter restore)
            {
                restore.WriteLine(profitLoss
                    + ", " + maxProfit
                    + ", " + maxLoss
                    + ", " + maxDrawdown
                    );
                restore.WriteLine(numContracts
                    + ", " + numWins
                    + ", " + numLoss
                    );
                restore.WriteLine(avgWin
                    + ", " + avgLoss
                    );
                restore.WriteLine(numOpen
                    + ", " + numDays
                    + ", " + numHours
                    + ", " + numTrades
                    + ", " + orderExits
                    + ", " + computeStats
                );

            }

            /// <summary>
            /// RestoreStats: loads Trading Statistics from "restore" file.
            /// <param name="restore">ref to System.IO.StreamReader</param>
            /// </summary>
            /// <returns>true if successful, otherwise throws exception.</returns>
            internal bool RestoreStats(ref System.IO.StreamReader restore)
            {
                try
                {
                    char[] delimiterChars = { ',', '\t' };
                    String line;
                    string[] vals;
                    string parseError = "Exception RestoreStats: parse error at ";
                    line = restore.ReadLine();
                    vals = line.Split(delimiterChars);
                    if (vals.Length != 4)
                        throw new Exception(" RestoreStats: profitLoss line format is incorrect !!  count=" + vals.Length);
                    if (!double.TryParse(vals[0], out profitLoss))
                        throw new Exception(parseError + "profitLoss");
                    if (!double.TryParse(vals[1], out maxProfit))
                        throw new Exception(parseError + "maxProfit");
                    if (!double.TryParse(vals[2], out maxLoss))
                        throw new Exception(parseError + "maxLoss");
                    if (!double.TryParse(vals[3], out maxDrawdown))
                        throw new Exception(parseError + "maxDrawdown");
                    line = restore.ReadLine();
                    vals = line.Split(delimiterChars);
                    if (vals.Length != 3)
                        throw new Exception(" RestoreStats: numContracts line format is incorrect !!  count=" + vals.Length);
                    if (!int.TryParse(vals[0], out numContracts))
                        throw new Exception(parseError + "numContracts");
                    if (!int.TryParse(vals[1], out numWins))
                        throw new Exception(parseError + "numWins");
                    if (!int.TryParse(vals[2], out numLoss))
                        throw new Exception(parseError + "numLoss");
                    line = restore.ReadLine();
                    vals = line.Split(delimiterChars);
                    if (vals.Length != 2)
                        throw new Exception(" RestoreStats: avgWin line format is incorrect !!  count=" + vals.Length);
                    if (!double.TryParse(vals[0], out avgWin))
                        throw new Exception(parseError + "avgWin");
                    if (!double.TryParse(vals[1], out avgLoss))
                        throw new Exception(parseError + "avgLoss");
                    line = restore.ReadLine();
                    vals = line.Split(delimiterChars);
                    if (vals.Length != 6)
                        throw new Exception(" RestoreStats: numOpen line format is incorrect !!  count=" + vals.Length);
                    vals = line.Split(delimiterChars);
                    if (!int.TryParse(vals[0], out numOpen))
                        throw new Exception(parseError + "numOpen");
                    if (!int.TryParse(vals[1], out numDays))
                        throw new Exception(parseError + "numDays");
                    if (!int.TryParse(vals[2], out numContracts))
                        throw new Exception(parseError + "numContracts");
                    if (!int.TryParse(vals[3], out numHours))
                        throw new Exception(parseError + "numHours");
                    if (!int.TryParse(vals[4], out orderExits))
                        throw new Exception(parseError + "orderExits");
                    if (!int.TryParse(vals[5], out computeStats))
                        throw new Exception(parseError + "computeStats");
                    return true;
                }
                catch (Exception e)
                {
                    var str = e.Message;
                    throw;
                }
            }
        }

        // Chart Bar data
        public class BarData
        {
            internal string symbol;
            internal DateTime BarTime;
            internal double open;
            internal double high;
            internal double low;
            internal double close;
            internal long volume;
            internal double bid;
            internal double ask;
            internal bool startOfSession;
            internal bool buySide;
            internal double typical;
            internal double change;
            internal double pctChange;
            internal double rawMF;
            internal double MFI;
            internal double spread;
            internal DayOfWeek wd;
            internal TimeSpan tod;
            internal double MDI;

            /// <summary>
            /// EA BarData Item Class
            /// </summary>
            public BarData()
            {
                BarData_Init();
            }

            /// <summary>
            /// BarData object constructor with Bar data
            /// </summary>
            public BarData(Bar ibar, double iBid = 0, double iAsk = 0)
            {
                BarData_Init();
                BarTime = ibar.BarTime;
                open = (double)ibar.Open;
                high = (double)ibar.High;
                low = (double)ibar.Low;
                close = (double)ibar.Close;
                volume = ibar.Volume;
                wd = BarTime.DayOfWeek;
                tod = BarTime.TimeOfDay;
                bid = iBid;
                ask = iAsk;
                if (bid * ask > 0)  // if bid and ask are both > 0
                    spread = ask - bid;
            }

            /// <summary>
            /// BarData initialization
            /// </summary>
            internal void BarData_Init()
            {
                symbol = "";
                BarTime = DateTime.UtcNow;
                high = 0;
                low = 0;
                close = 0;
                volume = -1;
                bid = 0;
                ask = 0;
                startOfSession = true;
                buySide = true;
                typical = 0;
                change = 0;
                pctChange = 0;
                rawMF = 0;
                MFI = 50;
                spread = 0;
                MDI = 0;
                wd = BarTime.DayOfWeek;
                tod = BarTime.TimeOfDay;
            }

            /// <summary>
            /// SaveBarData: save BarData to restore file
            /// </summary>
            public void SaveBarData(ref System.IO.StreamWriter restore)
            {
                restore.WriteLine(symbol);
                restore.WriteLine(this.BarTime
                    + ", " + open
                    + ", " + high
                    + ", " + low
                    + ", " + close
                    + ", " + volume
                    );
                restore.WriteLine(bid + ", " + ask);
                restore.WriteLine(wd + ", " + tod);
                restore.WriteLine(startOfSession + ", " + buySide);
                restore.WriteLine(typical
                   + ", " + change
                   + ", " + pctChange
                   + ", " + rawMF
                   + ", " + MFI
                   + ", " + spread
                   );
                restore.WriteLine(MDI);
            }

            // Restore Bar data fro restorefile
            public void RestoreBarData(ref System.IO.StreamReader restore, string firstline)
            {
                char[] delimiterChars = { ',', '\t' };
                String line;
                string[] vals;
                string parseError = "Exception RestoreBarData: parse error at ";
                //line = restore.ReadLine();
                vals = firstline.Split(delimiterChars);
                if (vals.Length != 1)
                    throw new Exception(parseError + "symbol");
                if (firstline != symbol)
                    throw new Exception(parseError + "symbol incorrect");
                this.symbol = firstline.Trim();
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 6)
                    throw new Exception(parseError + "BarTime");
                if (!DateTime.TryParse(vals[0], out BarTime))
                    throw new Exception(parseError + "BarTime");
                if (!double.TryParse(vals[1], out open))
                    throw new Exception(parseError + "open");
                if (!double.TryParse(vals[2], out high))
                    throw new Exception(parseError + "high");
                if (!double.TryParse(vals[3], out low))
                    throw new Exception(parseError + "low");
                if (!double.TryParse(vals[4], out close))
                    throw new Exception(parseError + "close");
                if (!long.TryParse(vals[5], out volume))
                    throw new Exception(parseError + "volume");
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 2)
                    throw new Exception(parseError + "bid");
                if (!double.TryParse(vals[0], out bid))
                    throw new Exception(parseError + "bid");
                if (!double.TryParse(vals[1], out ask))
                    throw new Exception(parseError + "ask");
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 2)
                    throw new Exception(parseError + "wd");
                if (!System.DayOfWeek.TryParse(vals[0], out wd))
                    throw new Exception(parseError + "wd");
                if (!TimeSpan.TryParse(vals[1], out tod))
                    throw new Exception(parseError + "tod");
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 2)
                    throw new Exception(parseError + "startOfSession");
                if (!bool.TryParse(vals[0], out startOfSession))
                    throw new Exception(parseError + "startOfSession");
                if (!bool.TryParse(vals[1], out buySide))
                    throw new Exception(parseError + "buySide");
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 6)
                    throw new Exception(parseError + "typical");
                if (!double.TryParse(vals[0], out typical))
                    throw new Exception(parseError + "typical");
                if (!double.TryParse(vals[1], out change))
                    throw new Exception(parseError + "change");
                if (!double.TryParse(vals[2], out pctChange))
                    throw new Exception(parseError + "pctChange");
                if (!double.TryParse(vals[3], out rawMF))
                    throw new Exception(parseError + "rawMF");
                if (!double.TryParse(vals[4], out MFI))
                    throw new Exception(parseError + "MFI");
                if (!double.TryParse(vals[5], out spread))
                    throw new Exception(parseError + "spread");
                line = restore.ReadLine();
                vals = line.Split(delimiterChars);
                if (vals.Length != 1)
                    throw new Exception(parseError + "MDI");
                if (!double.TryParse(vals[0], out MDI))
                    throw new Exception(parseError + "MDI");
            }
        }

        // Get most recent Bar time
        public DateTime GetCurTime()
        {
            if (simulate)
                return simTime;
            else // Alveo
                return Time[0];
        }

        // Get Chart currency Symbol
        internal string GetSymbol()
        {
            string theSymbol = "";
            if (!simulate)
            {
                theSymbol = Symbol();
            }
            else // simulate
            {
                if (pair == "")
                {
                    pair = "EUR/USD";
                }
                theSymbol = pair;
            }
            return theSymbol;
        }

        // Get Account Balance
        double GetAccoutBalance()
        {
            if (!simulate)
            {
                double accountBalance = AccountBalance();
                return accountBalance;
            }
            return simAccountBalance;  //simulated AccountBalance
        }

        // Print msg to Journal and Alveo Log file
        internal void JournalLog(string msg)
        {
            if (optimize)
                return;
            LogPrint(msg);
            if (!simulate)
                Journal(strategy + " [" + symbol + "]: " + msg);
        }

        /// <summary>
        /// LogPrint: print msg to Alveo Log or to Console
        /// </summary>
        /// <returns>void</returns>
        void LogPrint(string msg)
        {
            if (optimize)
                return;
            if (!simulate)
                Print(strategy + " [" + symbol + "]: " + msg);
            else
                Console.WriteLine(strategy + " [" + symbol + "]: " + msg);
        }

        /// <summary>
        /// Write the specified line to the Journal file
        /// </summary>
        internal void Journal(string line)
        {

            if (optimize || true)  // obsolete
                return;
            var filename = "Journal" + paramset + ".txt";
            System.IO.StreamWriter journal = new System.IO.StreamWriter(dataFileDir + filename, true);
            journal.WriteLine(DateTime.Now.ToString() + " " + strategy + ":  " + line);
            journal.Close();
        }

        // if value != flag, then change flag = value and return true, otherwise return flase (not changed)
        bool DetectChanged(ref bool flag, bool value)
        {
            bool changed;
            changed = (value != flag);
            if (changed)
            {
                flag = value;
            }
            return changed;
        }

        // checked conditions to determine if it is OK to Trade
        internal bool CheckOKToTrade()
        {
            try
            {
                riskLimit = 1.5 * accountBalance / 100 / 10; // 1.5 percent risk limit in Pips for 1 Standard lot

                if (accountBalance > 0 && accountBalance < 50.00)
                {
                    JournalLog(": balance is too low !! AccountBalance = " + accountBalance.ToString("F2"));
                    Sleep(1000);
                    return false;
                }

                if (DetectChanged(ref riskLimitReached, (s.startingBalance >= 0) ? accountBalance / s.startingBalance < 0.96 : false))
                {
                    if (riskLimitReached)
                    {
                        if (!optimize)
                            JournalLog(strategy + " Start: riskLimitReached, trading prevented. "
                            + " startingBalance =" + s.startingBalance.ToString("F2")
                            + " accountBalance =" + accountBalance.ToString("F2"));
                        closeAllTrades(reason: 5);
                        Sleep(1000); // sleep 1 sec
                        return false;
                    }
                }

                if (DetectChanged(ref tradingclosed, CheckTradingClosed()))
                {
                    if (tradingclosed)
                    {
                        closeAllTrades(reason: 6);
                        Sleep(1000); // sleep 1 sec
                        return false;
                    }
                    else // Trading resumed
                    {
                        var now = DateTime.Now;
                        if (!optimize)
                            JournalLog(": Trading Resumed. Time=" + now.ToShortTimeString());
                    }
                }

                if (!simulate)  // Alveo functions
                {
                    if (DetectChanged(ref s.isConnected, IsEAConnected()))
                    {
                        if (!s.isConnected)
                        {
                            LogAlert("ALERT " + strategy + " EA [" + symbol + "]: not connected!");
                            LogPrint(strategy + ": EA [" + symbol + "]: not connected!");
                            Journal(strategy + ": EA [" + symbol + "]: EA is not connected !   \n");
                            Sleep(1000); // sleep 1 sec
                            return false;  // not connected to Server
                        }
                        else // isConnected
                        {
                            LogPrint(strategy + ": EA [" + symbol + "]: is now connected!");
                            Journal(strategy + ": EA [" + symbol + "]: EA is now connected !  \n");
                        }
                    }

                    if (DetectChanged(ref s.isTradeAllowed, IsTradeAllowed()))
                    {
                        if (!s.isTradeAllowed)
                        {
                            Print(strategy + ": EA [" + symbol + "]: EA not allowed to Trade !");
                            Journal(strategy + ": EA [" + symbol + "]: EA not allowed to Trade !   \n");
                            closeAllTrades(reason: 7);
                            Sleep(1000); // sleep 1 sec
                            return false; // EA not allowed to Trade
                        }
                        else // isTradeAllowed
                        {
                            LogPrint(strategy + ": EA [" + symbol + "]: is now IsTradeAllowed!");
                            Journal(strategy + ": EA [" + symbol + "]: EA is now IsTradeAllowed !  \n");
                        }
                    }

                    if (DetectChanged(ref s.Stopped, IsEaStopped))
                    {
                        if (s.Stopped)
                        {
                            LogPrint(strategy + ": EA [" + symbol + "]: EA stopped !");
                            Journal(strategy + ": EA [" + symbol + "]: EA is stopped!   \n");
                            closeAllTrades(reason: 8);
                            Sleep(1000); // sleep 1 sec
                            return false; // EA Stopped
                        }
                        else
                        {
                            Print(strategy + ": EA [" + symbol + "]: EA resumed !");
                            Journal(strategy + ": EA [" + symbol + "]: EA is resumed!  \n");
                        }
                    }

                    if (IsEaStopped)
                    {
                        closeAllTrades(reason: 9);
                        Sleep(1000); // sleep 1 sec
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Print("CheckOKToTrade: Exception !!");
                Print(e.Message);
                Print(e.StackTrace);
                Sleep(500);
            }
            return true; //OKToTrade
        }

        // Get latest Chart Bar
        internal BarData GetCurBar()
        {
            BarData dI = null;
            if (simulate)
            {
                dI = curBar;  // data from Simulator
            }
            else // Alveo
            {
                Bar b = ChartBars[0];
                dI = new BarData(b, Bid, Ask);
            }
            return dI;
        }

        // Exit the specified Order at the specified price, for the specified reason
        void ExitOpenTrade(int reason, Order order, double price)
        {
            if (order == null)
                return;
            var orderID = (int)order.Id;
            var clsTime = GetOrderCloseTime(orderID);
            if (clsTime.DateTime.Year > 1970)
                return;  // already closed
            if (!optimize)
                LogPrint("ExitOpenTrade: order=" + orderID + " reason=" + reason);
            if (simulate && order.CloseDate == DateTime.MinValue)  // CloseDate not yet set
            {
                order.ClosePrice = (decimal)price;
                order.CloseDate = curTime;
            }
            if (!simulate) // Alveo
            {
                var lots = (double)order.Quantity;
                if (!optimize)
                    JournalLog("Closd order=" + orderID.ToString() + " lots=" + lots.ToString());
                if (OrderSelect(orderID, SELECT_BY_TICKET) == true)
                {
                    if (!optimize)
                        JournalLog("Open price for order #" + orderID.ToString() + " is " + OrderOpenPrice().ToString());
                }
                else // OrderSelect returned err
                {
                    var err = GetLastError();
                    if (!optimize)
                        JournalLog("ExitOpenTrade: ticket=" + orderID + "  !!! OrderSelect returned error of " + ErrorDescription(err));
                    return;
                }
                if (OrdersStatus() == (int)OrderStatus.PendingNew) // if Pending Order use OrderDelete
                {
                    if (!optimize)
                        JournalLog(" Deleting order=" + orderID.ToString());
                    var success = OrderDelete(orderID);
                    Sleep(500);
                    if (!success) // OrderDelete returned err
                    {
                        var err = GetLastError();
                        if (!optimize)
                            JournalLog("ExitOpenTrade: ticket=" + orderID + "  !!! OrderDelete returned error of " + ErrorDescription(err));
                        return;
                    }
                }
                else // !PendingNew then Open Order, use OrderClose
                {
                    OrderClose(orderID, lots, 0, 0);
                    Sleep(500);
                }
            }
            s.stats.orderExits++; // count orderExits

            var dProfit = TradeClosed(order); // compute Order statistics
            if (!simulate)
            {
                JournalLog("closeOpenTrade orderID=" + orderID.ToString() + "  reason=" + reason.ToString()
                    + " stoploss=" + order.StopLoss.ToString()
                    + "\n");
                JournalLog(" profitLoss=" + s.stats.profitLoss.ToString()
                    + " numWins=" + s.stats.numWins.ToString()
                    + " numLoss=" + s.stats.ToString()
                    + " maxDrawdown=" + s.stats.maxDrawdown.ToString()
                    );
                JournalOnExit(reason, order, bclose: (double)s.dI.close, dI: s.dI, dPofit: dProfit);
            }
            return;
        }

        // Specified Order was Closed, compute statistics
        public double TradeClosed(Order order)
        {
            if (order == null)
                return 0;
            double dPofit = (double)order.Quantity * GetPriceDiff(order) * pipPos * 10;  // (OrderClosePrice - OrderOpenPrice) * pipPos/10
            var orderID = (int)order.Id;
            if (!s.closedOrders.Keys.Contains(orderID))  // if orderID is not in closedOrders list
            {
                s.closedOrders.Add(orderID, order); // add Order to closedOrders list
                var side = order.Side;
                dPofit *= (side == TradeSide.Buy) ? 1.0 : -1.0;
                ComputeStats(dPofit); // compute statiustics
                order.Comment +=  // add closing data to order.Comment
                    "," + order.CloseDate.ToString()
                    + "," + order.ClosePrice.ToString("F5")
                    + "," + dPofit.ToString("F5");
                if (!optimize)
                {
                    LogPrint("TradeClosed: orderID=" + orderID + " side=" + order.Side + " dPofit=" + dPofit.ToString("F5"));
                    WriteTradeLog(order.Comment + ", " + simAccountBalance.ToString("F2")); // write traded data from order.Comment to TradeLog file
                }
            }
            return dPofit;
        }

        /// <summary>
        /// IsEAConnected: calls Alveo IsConnected()
        /// </summary>
        /// <returns>bool IsConnected</returns>
        bool IsEAConnected()
        {
            if (!simulate)
                return IsConnected();
            else
                return true;
        }

        // Generate Alveo Alert or write msg to PC Console
        void LogAlert(string msg)
        {
            if (!simulate)
                Alert(msg);
            else
                Console.WriteLine(msg);
        }

        // Close All trades for the specified reason and optionally clear buyOpenOrders and sellOpenOrders lists
        internal void closeAllTrades(int reason, bool clear = true)
        {
            if (!optimize)
                LogPrint("closeAllTrades: reason=" + reason + " curTime=" + curTime.ToShortDateString() + " " + curTime.ToLongTimeString());
            if (!simulate) // Alveo functions
            {
                int total = GetTotalOrders();
                Order order = null;
                for (int pos = 0; pos < total; pos++)
                {
                    if (OrderSelect(pos, SELECT_BY_POS) == false)
                        continue;
                    var ticket = OrderTicket();  // get ticket (order.Id)
                    if (s.buyOpenOrders.ContainsKey(ticket))  // get Order data from buyOpenOrders list
                    {
                        order = s.buyOpenOrders[ticket];
                    }
                    else if (s.sellOpenOrders.ContainsKey(ticket)) // get Order data from sellOpenOrders list
                    {
                        order = s.sellOpenOrders[ticket];
                    }
                    else
                        return;
                    ExitOpenTrade(reason, order, curPrice);  // Exit the specified Order for the specified reason at the curPrice
                }
            }
            else // simulate functions
            {
                if (s.buyOpenOrders.Count > 0)
                    foreach (var order in s.buyOpenOrders.Values)
                    {
                        ExitOpenTrade(reason, order, curPrice); // Exit the specified Order for the specified reason at the curPrice
                    }
                if (s.sellOpenOrders.Count > 0)
                    foreach (var order in s.sellOpenOrders.Values)
                    {
                        ExitOpenTrade(reason, order, curPrice); // Exit the specified Order for the specified reason at the curPrice
                    }
            }
            if (clear)  // Clear order lists
            {
                s.buyOpenOrders.Clear();
                s.sellOpenOrders.Clear();
            }
            if (!simulate)
                Sleep(1000); // sleep 1 sec
            return;
        }

        // if Time to Exit, then return true; otherwise false
        internal bool CheckTradingClosed()
        {
            bool closed = false;
            if (s.dI != null)
            {
                closed = ((s.dI.wd == System.DayOfWeek.Friday && s.dI.tod > fridayclose)
                    || s.dI.wd == System.DayOfWeek.Saturday
                    || (s.dI.wd == System.DayOfWeek.Sunday && s.dI.tod < sundayOpen)
                    || (s.dI.tod > dailyMaintStart && s.dI.tod < dailyMaintEnd));
            }
            return closed;
        }

        // convert Alveo int err to string errorDescription
        string ErrorDescription(int err)
        {
            string errorDescription = "err# " + err.ToString();
            switch (err)
            {
                case 0:
                    errorDescription += " ERR_NO_ERROR";
                    break;
                case 1:
                    errorDescription += " ERR_NO_RESULT";
                    break;
                case 2:
                    errorDescription += " ERR_COMMON_ERROR";
                    break;
                case 3:
                    errorDescription += " ERR_INVALID_TRADE_PARAMETERS";
                    break;
                case 4:
                    errorDescription += " ERR_SERVER_BUSY";
                    break;
                case 5:
                    errorDescription += " ERR_OLD_VERSION";
                    break;
                case 6:
                    errorDescription += " ERR_NO_CONNECTION";
                    break;
                case 7:
                    errorDescription += " ERR_NOT_ENOUGH_RIGHTS";
                    break;
                case 8:
                    errorDescription += " ERR_TOO_FREQUENT_REQUESTS";
                    break;
                case 9:
                    errorDescription += " ERR_MALFUNCTIONAL_TRADE";
                    break;
                case 64:
                    errorDescription += " ERR_ACCOUNT_DISABLED";
                    break;
                case 65:
                    errorDescription += " ERR_INVALID_ACCOUNT";
                    break;
                case 128:
                    errorDescription += " ERR_TRADE_TIMEOUT";
                    break;
                case 129:
                    errorDescription += " ERR_INVALID_PRICE";
                    break;
                case 130:
                    errorDescription += " ERR_INVALID_STOPS";
                    break;
                case 131:
                    errorDescription += " ERR_INVALID_TRADE_volume";
                    break;
                case 132:
                    errorDescription += " ERR_MARKET_closed";
                    break;
                case 133:
                    errorDescription += " ERR_TRADE_DISABLED";
                    break;
                case 134:
                    errorDescription += " ERR_NOT_ENOUGH_MONEY";
                    break;
                case 135:
                    errorDescription += " ERR_PRICE_CHANGED";
                    break;
                case 136:
                    errorDescription += " ERR_OFF_QUOTES";
                    break;
                case 137:
                    errorDescription += " ERR_BROKER_BUSY";
                    break;
                case 138:
                    errorDescription += " ERR_REQUOTE";
                    break;
                case 139:
                    errorDescription += " ERR_ORDER_LOCKED";
                    break;
                case 140:
                    errorDescription += " ERR_LONG_POSITIONS_ONLY_ALlowED";
                    break;
                case 141:
                    errorDescription += " ERR_TOO_MANY_REQUESTS";
                    break;
                case 145:
                    errorDescription += " ERR_TRADE_MODIFY_DENIED";
                    break;
                case 146:
                    errorDescription += " ERR_TRADE_CONTEXT_BUSY";
                    break;
                case 147:
                    errorDescription += " ERR_TRADE_EXPIRATION_DENIED";
                    break;
                case 148:
                    errorDescription += " ERR_TRADE_TOO_MANY_ORDERS";
                    break;
                case 149:
                    errorDescription += " ERR_TRADE_HEDGE_PROHIBITED";
                    break;
                case 150:
                    errorDescription += " ERR_TRADE_PROHIBITED_BY_FIFO";
                    break;
                case 4000:
                    errorDescription += " ERR_NO_MQLERROR";
                    break;
                case 4001:
                    errorDescription += " ERR_WRONG_FUNCTION_POINTER";
                    break;
                case 4002:
                    errorDescription += " ERR_ARRAY_INDEX_OUT_OF_RANGE";
                    break;
                case 4003:
                    errorDescription += " ERR_NO_MEMORY_FOR_CALL_STACK";
                    break;
                case 4013:
                    errorDescription += " ERR_ZERO_DIVIDE";
                    break;
                case 4014:
                    errorDescription += " ERR_UNKNOWN_COMMAND";
                    break;
                case 4015:
                    errorDescription += " ERR_WRONG_JUMP";
                    break;
                case 4016:
                    errorDescription += " ERR_NOT_INITIALIZED_ARRAY";
                    break;
                case 4017:
                    errorDescription += " ERR_DLL_CALLS_NOT_ALlowED";
                    break;
                case 4018:
                    errorDescription += " ERR_CANNOT_LOAD_LIBRARY";
                    break;
                case 4019:
                    errorDescription += " ERR_CANNOT_CALL_FUNCTION";
                    break;
                case 4020:
                    errorDescription += " ERR_EXTERNAL_CALLS_NOT_ALlowED";
                    break;
                case 4021:
                    errorDescription += " ERR_NO_MEMORY_FOR_RETURNED_STR";
                    break;
                case 4022:
                    errorDescription += " ERR_SYSTEM_BUSY";
                    break;
                case 4051:
                    errorDescription += " ERR_INVALID_FUNCTION_PARAMVALUE";
                    break;
                case 4062:
                    errorDescription += " ERR_STRING_PARAMETER_EXPECTED";
                    break;
                case 4063:
                    errorDescription += " ERR_INTEGER_PARAMETER_EXPECTED";
                    break;
                case 4064:
                    errorDescription += " ERR_DOUBLE_PARAMETER_EXPECTED";
                    break;
                case 4065:
                    errorDescription += " ERR_ARRAY_AS_PARAMETER_EXPECTED";
                    break;
                case 4066:
                    errorDescription += " ERR_HISTORY_WILL_UPDATED";
                    break;
                case 4067:
                    errorDescription += " ERR_TRADE_ERROR";
                    break;
                case 4105:
                    errorDescription += " ERR_NO_ORDER_SELECTED";
                    break;
                case 4106:
                    errorDescription += " ERR_UNKNOWN_SYMBOL";
                    break;
                case 4107:
                    errorDescription += " ERR_INVALID_PRICE_PARAM";
                    break;
                case 4108:
                    errorDescription += " ERR_INVALID_TICKET";
                    break;
                case 4109:
                    errorDescription += " ERR_TRADE_NOT_ALLOWED";
                    break;
                default:
                    errorDescription += " unknown_err_code " + err.ToString();
                    break;
            }
            return errorDescription;
        }

        // compute Order Statistics
        internal void ComputeStats(double dPofit)
        {
            s.stats.profitLoss += dPofit;  // update profitLoss
            s.stats.numContracts++;  // update numContracts
            simAccountBalance += dPofit;

            // calculate avgWin, avgLoss, numWins, numLoss, maxProfit, maxLoss, maxDrawdown
            if (dPofit >= 0)
            {
                s.stats.avgWin = (s.stats.numWins * s.stats.avgWin + Math.Abs(dPofit)) / (s.stats.numWins + 1);
                s.stats.numWins++;
            }
            else // (dPofit <= 0), Loss
            {
                s.stats.avgLoss = (s.stats.numLoss * s.stats.avgLoss + Math.Abs(dPofit)) / (s.stats.numLoss + 1);
                s.stats.numLoss++;
            }
            if (dPofit > s.stats.maxProfit)
                s.stats.maxProfit = dPofit;
            else if (dPofit < s.stats.maxLoss)
                s.stats.maxLoss = dPofit;
            if (s.stats.profitLoss < s.stats.maxDrawdown) // compute maxDrawdown
                s.stats.maxDrawdown = s.stats.profitLoss;
            s.stats.computeStats++; // count num Order statistics computed
        }

        // Genreate Journel entries for specified exited order
        void JournalOnExit(int reason, Order order, double bclose, BarData dI,
        double t2 = 0, double t5 = 0, double dPofit = 0, double stoploss = 0)
        {
            if (optimize || simulate || order == null)
                return;
            var side = order.Side;

            double openPrice = (double)order.OpenPrice;
            switch (reason)
            {
                case 1: // take profit on Buy order
                    Journal("ExitOpenTrade: reason = take profit on Buy order.");
                    Journal(" order=" + order.Id + " openPrice=" + openPrice.ToString("F5")
                        + " close=" + bclose.ToString("F5") + " dPofit=" + dPofit.ToString("F5")
                    );
                    break;
                case 2: // stoploss on Buy order
                    Journal("ExitOpenTrade: reason = stoploss on Buy order.");
                    Journal(" order=" + order.Id + " openPrice=" + openPrice.ToString("F5")
                        + " close=" + bclose.ToString("F5") + " dPofit=" + dPofit.ToString("F5"));
                    break;
                case 3: // take profit on Sell order
                    Journal("ExitOpenTrade: reason = take profit on Sell order.");
                    Journal(" order=" + order.Id + " openPrice=" + openPrice.ToString("F5")
                        + " close=" + bclose.ToString("F5") + " dPofit=" + dPofit.ToString("F5")
                    );
                    break;
                case 4: // stoploss on Sell order
                    Journal("ExitOpenTrade: reason = stoploss on Sell order.");
                    Journal(" order=" + order.Id + " openPrice=" + openPrice.ToString("F5")
                        + " close=" + bclose.ToString("F5") + " dPofit=" + dPofit.ToString("F5"));
                    break;
                case 5: // closeAllTrades: tradingclosed || riskLimitReached
                    Journal("closeAllTrades: reason = tradingclosed || riskLimitReached close=" + bclose.ToString());
                    Journal(" dI.Date=" + dI.BarTime.ToShortDateString() + " " + dI.BarTime.ToLongTimeString() + " dPofit=" + dPofit.ToString("F5"));
                    Journal(" tradingclosed=" + tradingclosed + " riskLimitReached=" + riskLimitReached);
                    break;
                case 6: // closeAllTrades: timeToExit
                    Journal("closeOpenTrade: reason = timeToExit  close=" + bclose.ToString());
                    Journal(" dI.Date=" + dI.BarTime.ToShortDateString() + " " + dI.BarTime.ToLongTimeString() + " dPofit=" + dPofit.ToString("F5"));
                    break;
                case 7: // closeAllTrades: !isTradeAllowed
                    Journal("closeOpenTrade: reason = !isTradeAllowed. close=" + bclose.ToString() + " isTradeAllowed=" + isTradeAllowed.ToString());
                    Journal(" dI.Date=" + dI.BarTime.ToShortDateString() + " " + dI.BarTime.ToLongTimeString() + " dPofit=" + dPofit.ToString("F5"));
                    break;
                case 8: // closeAllTrades: IsEaStopped
                    Journal("closeOpenTrade: reason = IsEaStopped.  bclose=" + bclose.ToString() + " IsEaStopped=" + IsEaStopped.ToString());
                    Journal(" dI.Date=" + dI.BarTime.ToShortDateString() + " " + dI.BarTime.ToLongTimeString() + " dPofit=" + dPofit.ToString("F5"));
                    break;
                case 9: // IsEaStopped
                    Journal("closeOpenTrade: reason = IsEaStopped.  bclose=" + bclose.ToString() + " IsEaStopped=" + IsEaStopped.ToString());
                    Journal(" dI.Date=" + dI.BarTime.ToShortDateString() + " " + dI.BarTime.ToLongTimeString() + " dPofit=" + dPofit.ToString("F5"));
                    break;
                case 99:  // closeAllTrades at end of simulation
                    Journal("closeOpenTrade: reason = closeAllTrades at end of simulation. close=" + bclose.ToString());
                    Journal(" dI.Date=" + dI.BarTime.ToShortDateString() + " " + dI.BarTime.ToLongTimeString() + " dPofit=" + dPofit.ToString("F5"));
                    break;
                default:
                    throw new Exception("Exception: JournalOnclose reason not defined !! ");
                    //break;
            }
        }

        // Get Total number of Open orders for this Symbol
        internal int GetTotalOrders()
        {
            int total = 0;
            if (!simulate) // Alveo function
            {
                var cnt = OrdersTotal();
                for (int i = 0; i < cnt; i++)
                {
                    if (OrderSelect(i, SELECT_BY_POS) == false)
                        continue;
                    if (OrderSymbol() == symbol)  // symbol matched
                        total++;
                }
                return total;
            }
            else // simulate
            {
                total = s.buyOpenOrders.Count + s.sellOpenOrders.Count;
            }
            return total;
        }

        // return OrderCloseTime if closed, else return datetime0
        internal datetime GetOrderCloseTime(int ticketID)
        {
            Order order = null;
            datetime closeTime = datetime0;
            if (!simulate)
            {
                if (OrderSelect(ticketID, SELECT_BY_TICKET) == true)
                    closeTime = OrderCloseTime();
                else
                    closeTime = datetime0;
            }
            else // simulate
            {
                try
                {
                    if (s.buyOpenOrders.Count > 0)
                        if (s.buyOpenOrders.Keys.Contains(ticketID))
                            order = s.buyOpenOrders[ticketID];
                }
                catch (KeyNotFoundException) // order not in buyOpenOrders
                {
                    order = null;
                }
                if (order == null)
                {
                    try
                    {
                        if (s.sellOpenOrders.Count > 0)
                            if (s.sellOpenOrders.Keys.Contains(ticketID))
                                order = s.sellOpenOrders[ticketID];
                    }
                    catch (KeyNotFoundException) // order not in sellOpenOrders
                    {
                        order = null;
                    }
                }
                if (order != null) // order found
                {
                    if (order.CloseDate > DateTime.MinValue)  // order.CloseDate not default value
                        closeTime = order.CloseDate;
                    else
                        closeTime = datetime0;
                }
            }
            return closeTime;
        }

        // return OrderClosePrice - OrderOpenPrice
        internal double GetPriceDiff(Order order)
        {
            double priceDiff = 0;
            if (!simulate) // Alveo functions
                priceDiff = OrderClosePrice() - OrderOpenPrice();
            else
                priceDiff = (double)(order.ClosePrice - order.OpenPrice);
            return priceDiff;
        }

        // Open a new Market Order in the targetDir with orderSL and orderTP
        public int CreateOrder(string comment = "")
        {
            int cmd;
            double price;
            double orderSL;
            double orderTP;
            double ask = GetMarketInfo(symbol, MODE_ASK);
            double bid = GetMarketInfo(symbol, MODE_BID);
            double stoplossLMT = Math.Min((double)stoploss, riskLimit / Math.Abs(orderQty));
            double takeprofitLMT = (double)takeprofit;
            if (s.targetDir > 0)  // Buy
            {
                price = ask;
                cmd = OP_BUY;
                orderSL = price - stoplossLMT / pipPos;
                orderTP = price + takeprofitLMT / pipPos;
            }
            else // Sell
            {
                price = bid;
                cmd = OP_SELL;
                orderSL = price + stoplossLMT / pipPos;
                orderTP = price - takeprofitLMT / pipPos;
            }

            if (JISO > 0) // if JISO specified, Open #JISO additional orders with TPincr spread
            {
                if (!optimize)
                    LogPrint("CreateOrder: JISO=" + JISO);
                int nJISO = (int)Math.Round(Math.Min(JISO, takeprofit / 0.1 - 1));  // at lease 1 Point TP separation
                double TPincr = Math.Min(takeprofitLMT / ((double)nJISO + 1), 1.0);
                double JISOtp = 0;
                for (int i = 0; i < nJISO; i++) // open nJISO additional orders
                {
                    JISOtp += Math.Round(TPincr, 1);
                    var orderTP2 = orderTP - ((s.targetDir > 0) ? 1 : -1) * JISOtp / pipPos;
                    var volume = orderQty;
                    if (farPrice > 0 && orderQty > 0.01 && AvgPriceFilter > 0)
                    {
                        var distance = curPrice - s.averagerPrice;
                        if (Math.Abs(distance) > farPrice / pipPos)
                        {
                            if (cmd == OP_BUY)
                            {
                                if (distance > 0)
                                    volume -= 0.0;
                                else
                                    volume += 0.01;
                            }
                            else if (cmd == OP_SELL)
                            {
                                if (distance > 0)
                                    volume += 0.01;
                                else
                                    volume -= 0.0;
                            }
                        }
                    }
                    volume = Math.Max(Math.Round(volume, 2), 0.01);
                    TryCreateOrder(cmd, volume, price, orderSL, orderTP2, comment);
                }
            }
            int ticket = TryCreateOrder(cmd, orderQty, price, orderSL, orderTP, comment);  // open original order with TP = orderTP
            return ticket;
        }

        // try to create order with the specified parameters
        public int TryCreateOrder(int cmd, double orderQty, double price, double orderSL = 0, double orderTP = 0, string comment = "", int magic = 0, color clr = null)
        {
            var digits = GetDigits();  // Normalize parameters to digits decimal places for Alveo function
            price = NormalizeDouble(price, digits);
            orderSL = NormalizeDouble(orderSL, digits);
            orderTP = NormalizeDouble(orderTP, digits);
            orderQty = NormalizeDouble(Math.Abs(orderQty), 2);
            int ticket = -1;  // dummy ticket number
            s.ordersThisHour++;  // increment ordersThisHour count
            ticket = SendOrder(cmd, orderQty, price, 0, orderSL, orderTP, comment: comment);
            if (!optimize)
                LogPrint("CreateOrder: Order created. Id=" + ticket + " Qty=" + orderQty + " Side=" + cmd + " price=" + price);
            s.stats.numTrades++;  // increment numTrades count
            return ticket;
        }

        // Get specified MarketInfo Item
        public double GetMarketInfo(string symbol, int item)
        {
            switch (item)
            {
                case MODE_BID:
                    return (simulate) ?
                        curPrice - .2 / pipPos
                        : Bid;  // Alveo value
                case MODE_ASK:
                    return (simulate) ?
                        curPrice + .2 / pipPos
                        : Ask; // Alveo value
                default:
                    return 0; // unknown item
            }
        }

        // Get Normalize Digits from Alveo
        internal int GetDigits()
        {
            if (!simulate)
                return Digits;  // Alveo function
            else
                return 5;
        }

        // Send OrderSend request to Alveo
        internal int SendOrder(int cmd, double volume, double price, int slippage = 0, double stoploss = 0, double takeprofit = 0, string comment = "", int magic = 0, color clr = null)
        {
            if (price <= 0)
                throw new Exception("SendOrder: invalid price !!");
            int ticket = -1;
            if (!simulate)  // Alveo functions
            {
                int err = 0;
                int cnt = 0;
                while (ticket < 0 && err != 4109 && err != 132 && err != 133)
                {
                    ticket = OrderSend(symbol, cmd, volume, price, slippage, stoploss, takeprofit);
                    if (ticket < 0) // Alveo returned err
                    {
                        err = GetLastError();
                        if (err == 4108)
                            Print("about line 280 OrderSend failed !!");
                        cnt++;
                        if (cnt > 10)  // try up to 10 times before Exception
                            throw new Exception("SendOrder: OrderSend failed !! err=" + err.ToString());
                    }
                    Sleep(2000); // Sleep 2 sec between OrderSends
                }
            }
            else // simulate, Simulator, not Alveo
            {
                ticket = ++ticketNum;  // update simulated ticketNum
            }
            if (ticket >= 0)  // ticket valid, create new Order
            {
                Order order = new Order();
                order.Id = ticket;
                order.Symbol = symbol;
                order.Quantity = (decimal)volume;
                order.Type = TradeType.Market;
                order.StopLoss = (decimal)stoploss;
                order.TakeProfit = (decimal)takeprofit;
                order.OpenDate = GetCurTime();
                order.OpenPrice = (decimal)price;
                order.CloseDate = DateTime.MinValue;
                if (cmd == OP_BUY)
                {
                    order.Side = TradeSide.Buy;
                    s.buyOpenOrders.Add(ticket, order);  // add to buyOpenOrders list
                }
                else if (cmd == OP_SELL)
                {
                    order.Side = TradeSide.Sell;
                    s.sellOpenOrders.Add(ticket, order);  // add to sellOpenOrders list
                }
                if (!optimize) // save Oped data in order.Comment and LogPrint info
                {
                    string msg = String.Format("{0},{1},{2},{3},{4},{5},{6},{7}",
                                     ((cmd == OP_BUY) ? "B" : "S"),
                                     order.Id.ToString(),
                                     order.Quantity.ToString(),
                                     order.OpenDate.ToString(),
                                     order.OpenPrice.ToString("F5"),
                                     order.StopLoss.ToString("F5"),
                                     order.TakeProfit.ToString("F5"),
                                     comment);
                    order.Comment = msg;
                    LogPrint("SendOrder: Opened order=" + order.Id
                        + " OpenPrice=" + order.OpenPrice.ToString("F5")
                        + " TakeProfit=" + order.TakeProfit.ToString("F5"));
                }
            }
            return ticket;  // unique ticket number
        }

        // Modify Order StopLoss limit
        internal bool ModifyOrder(Order order, double stoploss)
        {
            bool res = false;
            int err = 0;
            int cnt = 0;
            if (order.CloseDate.Year > 1970)
                return false;  // already closed
            int ticket = (int)order.Id;
            if (!simulate)  // Alveo functions
            {
                OrderSelect(ticket, SELECT_BY_TICKET);
                if (OrderStopLoss() - stoploss > 1 / pipPos)
                {
                    while (!res && err != 4109 && err != 132 && err != 133)
                    {
                        res = OrderModify(OrderTicket(),
                            OrderOpenPrice(),
                            NormalizeDouble(stoploss, Digits),
                            OrderTakeProfit(), 0);

                        if (!res) // Alveo returned err
                        {
                            err = GetLastError();
                            if (err == 4108)
                                Print("about line 280 OrderSend failed !!");
                            cnt++;
                            if (cnt > 10)  // try up to 10 times before Exception
                                throw new Exception("SendOrder: OrderModify failed !! err=" + err.ToString());
                        }
                        else
                        {
                            if (!optimize)
                                JournalLog(" ModifyOrder: ticket=" + ticket + " stoploss=" + stoploss.ToString("F5"));
                        }
                        Sleep(500); // Sleep 0.5 sec between OrderModify
                    }
                }
                else  // no need to change stoploss
                    res = true;
            }
            return res;  // unique ticket number
        }

        // extrenal interface to get EA Statistics
        public Statistics GetStats()
        {
            return s.stats;
        }

        // for optimizing, load trading parameters from file
        void LoadParameters()
        {
            string path;
            string parseErr = "Exception LoadBestParameters: parse error at ";
            string paramFilename = "ParamSet" + symbol.Replace("/", "") + paramset.ToString() + ".csv";
            path = dataFileDir + paramFilename;
            if (!System.IO.File.Exists(path))
            {
                LogPrint("EA: File " + path + " does not exist !!");
                throw new Exception("Exception: File " + path + " does not exist !!");
            }
            System.IO.StreamReader best = new System.IO.StreamReader(path);
            LogPrint("EA: Reading File " + path);
            var line = best.ReadLine();
            line = best.ReadLine();
            char[] delimiterChars = { ',', '\t' };
            string[] vals = line.Split(delimiterChars);
            if (vals.Count() != 18)
                throw new Exception("Exception: File " + path + " improper format !!");
            // var digits = GetDigits();
            strategy = vals[0];
            int X;
            if (!int.TryParse(vals[4], out X))
                throw new Exception(parseErr + "stoploss !!");
            stoploss = X;
            if (!int.TryParse(vals[5], out X))
                throw new Exception(parseErr + "takeprofit !!");
            takeprofit = X;
            if (!int.TryParse(vals[6], out X))
                throw new Exception(parseErr + "MDIperiod !!");
            MDIperiod = (double)X;
            if (!int.TryParse(vals[7], out X))
                throw new Exception(parseErr + "slopeThreshold !!");
            slopeThreshold = X;
            best.Close();
            return;
        }
        internal TimeFrame GetTimeframe()
        {
            TimeFrame tf = TimeFrame.M1;
            if (!simulate)
                tf = Chart.TimeFrame;
            else
                tf = simTimeframe;
            return tf;
        }
    }
}