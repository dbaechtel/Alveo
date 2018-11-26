/*
 * * LEGAL DISCLAIMER * *

The currency markets can do ANYTHING at ANY TIME.
No one can guarantee or forecast how these results will perform or behave in future markets.
Anyone who uses this product or this information is responsible for deciding If, Where, When and How this product and information are used.
Anyone who uses this product or this information is responsible and liable for any outcomes that might result from the use of this product or this information.
There is no warranty or guarantee provided or implied for this product or this information for any purpose.
 */

using System;
using System.ComponentModel;
using System.Windows.Media;
using Alveo.Interfaces.UserCode;
using Alveo.UserCode;
using Alveo.Common;
using Alveo.Common.Classes;
using Alveo.Common.Enums;

namespace Alveo.UserCode
{
    [Serializable]
    [Description("Move StopLoss")]
    public class MoveSL : ScriptBase
    {
        string Version = "1.0";

        [Category("Settings")]
        [Description("Stoploss limit in base currecy. [ex: 1.12345]")]
        public double StopLoss { get; set; }

        string symbol;                  // chart symbol
        string magicStr;                // used to identify orders
        string timeFrame;               // Chart.Timegrame
        string strategy;

        public MoveSL()
        {
            copyright = "(C) 2018, Entity3 LLC";
            link = "";
            StopLoss = 0;
        }

        //+------------------------------------------------------------------+"
        //| script program start function called by Alveo                    |"
        //+------------------------------------------------------------------+"
        protected override int Start()
        {
            try
            {
                RefreshRates();
                strategy = this.Name;
                symbol = Chart.Symbol;
                timeFrame = Chart.TimeFrame.ToString();
                magicStr = strategy + "," + symbol + "," + timeFrame;
                Print(magicStr + ": Version " + Version + " Script started.");
                if (StopLoss <= 0)
                {
                    Print(magicStr + ": StopLoss <= 0 !");
                    return 0;
                }
                int total = OrdersTotal();
                for (int i = 0; i < total; i++) // iterate through all Open orders
                {
                    if (OrderSelect(i, SELECT_BY_POS))  // order i selected
                    {
                        int ticket = OrderTicket();
                        var OpenPrice = OrderPendingPrice();
                        var type = OrderType();
                        if (OpenPrice <= 0)
                        {
                            Print(magicStr + ": OpenPrice is too low !! OpenPrice=" + OpenPrice);
                            continue;
                        }
                        if (type == OP_BUYSTOP)
                        {
                            if (OpenPrice - StopLoss < 0.0001)
                            {
                                Print(magicStr + ": StopLoss is too high !!   OpenPrice=" + OpenPrice + " StopLoss=" + StopLoss);
                                continue;
                            }
                        }
                        else if (type == OP_SELLSTOP)
                        {
                            if (StopLoss - OpenPrice < 0.0001)
                            {
                                Print(magicStr + ": StopLoss is too low !!   OpenPrice=" + OpenPrice + " StopLoss=" + StopLoss);
                                continue;
                            }

                        }
                        DateTime CloseDate = OrderCloseTime();
                        var sym = OrderSymbol();
                        if (sym == symbol && CloseDate.Year < 1980)  // symbol correct and !closed
                        {
                            var sl = NormalizeDouble(StopLoss, Digits);
                            Print(magicStr + ": ticket=" + ticket + " OpenPrice=" + OpenPrice + " sl=" + sl);
                            var success = OrderModify(ticket, OpenPrice, sl, 0, null);
                            if (success)
                                Print(magicStr + ": OrderModify: success. Moved SL ticket=" + ticket + " to StopLoss=" + sl);
                            else    // !success
                            {
                                var err = GetLastError();
                                Print(magicStr + ": OrderModify: failed. ticket=" + ticket + " err=" + err + " StopLoss=" + sl);
                            }
                        }
                    }
                }
                Print(magicStr + ": Script done.");
            }
            catch (Exception e)
            {
                Print(magicStr + " Exception " + e.Message);
                Print(magicStr + " Exception " + e.StackTrace);
            }
            return 0;
        }
    }
}
