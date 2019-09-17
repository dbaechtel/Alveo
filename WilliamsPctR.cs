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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Alveo.Interfaces.UserCode;
using Alveo.Common.Classes;
using Alveo.UserCode;
using Alveo.Common;

namespace Alveo.UserCode
{
    [Serializable]
    [Description("")]
    public class WilliamsPctR : IndicatorBase
    {
        public enum PriceTypes
        {
            PRICE_CLOSE = 0,
            PRICE_OPEN = 1,
            PRICE_HIGH = 2,
            PRICE_LOW = 3,
            PRICE_MEDIAN = 4,
            PRICE_TYPICAL = 5,
            PRICE_WEIGHTED = 6,
            PRICE_OHLC = 7,
            PRICE_P7 = 8
        }

        #region Properties
        Array<double> PctR;
        Array<double> UpperLine;
        Array<double> LowerLine;
        WillamsPctRobj pctR;
        int e;
        Bar b;              // holds latest chart Bar data from Alveo
        Bar prevBar;        // holds latest chart Bar data from Alveo
        bool firstrun;
        #endregion

        public WilliamsPctR()
        {
            // Basic indicator initialization. Don't use this constructor to calculate values
            copyright = "(C)2019 Entity3 LLC";
            link = "";
            Period = 14;
            PriceType = 0;
            indicator_chart_window = false;
            indicator_buffers = 3;
            PctR = new Array<double>();
            UpperLine = new Array<double>();
            LowerLine = new Array<double>();
            pctR = null;
            IndicatorBuffers(indicator_buffers);
            SetIndexBuffer(0, PctR);
            SetIndexArrow(0, 159);
            SetIndexBuffer(1, UpperLine);
            SetIndexArrow(1, 159);
            SetIndexBuffer(2, LowerLine);
            SetIndexArrow(2, 159);
            SetIndexStyle(0, DRAW_LINE, STYLE_SOLID, clr: Colors.Red);
            SetIndexStyle(1, DRAW_LINE, STYLE_SOLID);
            SetIndexLabel(1, "Upper");
            SetIndexStyle(2, DRAW_LINE, STYLE_SOLID);
            SetIndexLabel(2, "Lower");
            SetIndexStyle(3, DRAW_ARROW, STYLE_SOLID, clr: Colors.Orange);
            indicator_width1 = 2;
            indicator_width2 = 2;
            indicator_width3 = 2;
            indicator_color1 = Colors.Red;
            indicator_color2 = Colors.White;
            indicator_color3 = Colors.White;
            IndicatorBuffers(indicator_buffers);
        }

        [Category("Settings")]
        [DisplayName("MA Period")]
        public int Period { get; set; }

        [Category("Settings")]
        [DisplayName("")]
        [Description("")]
        public PriceTypes PriceType { get; set; }

        //+------------------------------------------------------------------+");
        //| Custom indicator initialization function                         |");
        //+------------------------------------------------------------------+");
        protected override int Init()
        {
            try  // to catch and handle Exceptions that might occur in this code block
            {
                IndicatorBuffers(indicator_buffers);
                IndicatorShortName("Willams %R(" + Period + ")");
                SetIndexLabel(0, "Willams Pct R(" + Period + ")");
                SetIndexBuffer(0, PctR);
                SetIndexArrow(0, 159);
                SetIndexBuffer(1, UpperLine);
                SetIndexArrow(1, 159);
                SetIndexBuffer(2, LowerLine);
                SetIndexArrow(2, 159);
                pctR = new WillamsPctRobj(this, Period);
                b = null;
                prevBar = null;
            }
            catch (Exception ex)    // catch and Print any exceptions that may have happened
            {
                Print("WilliamsPctR: Start: Exception: " + ex.Message);
                Print("WilliamsPctR: " + ex.StackTrace);
            }
            return 0;
        }

        //+------------------------------------------------------------------+");
        //| Custom indicator deinitialization function                       |");
        //+------------------------------------------------------------------+");
        protected override int Deinit()
        {
            // ENTER YOUR CODE HERE
            return 0;
        }

        //+------------------------------------------------------------------+");
        //| Custom indicator iteration function                              |");
        //+------------------------------------------------------------------+");
        protected override int Start()
        {
            try  // to catch and handle Exceptions that might occur in this code block
            {
                e = Bars - 1;   // e = largest index in ChartBars array
                if (e < 2)      // not enough data
                    return -1;

                prevBar = b;
                if (firstrun)   // on first run, calculate HMA on all ChartBars data
                {
                    b = ChartBars[e - 1];           // b = refernec to oldest ChartBars data
                    prevBar = b;
                    firstrun = false;               // firstrun initialization completed 
                }
                else // not firstrun; only calculate HEMA on ChartBars not already processed
                {
                    var counted_bars = IndicatorCounted();
                    if (counted_bars < 0)
                        throw new Exception("WilliamsPctR: Start: invalid IndicatorCounted value.");            // invalid value
                    e -= counted_bars;          // reduce e count by bars previously processed
                }
                if (e < 1)                      // no data to process
                    return 0;                   // we're out of here, for now    

                for (int i = 0; i < e; i++)     // iterate each bar to process  
                {
                    b = ChartBars[e - i - 1];   // get oldest chart bar in array
                    if (prevBar == null)
                        prevBar = b;
                    double thePrice = GetThePrice((int)PriceType, b);
                    pctR.Calc(thePrice);
                    UpdateBuffers(i);                     // update Alvelo Indicator buffers
                }
            }
            catch (Exception ex)    // catch and Print any exceptions that may have happened
            {
                Print("WilliamsPctR: Start: Exception: " + ex.Message);
                Print("WilliamsPctR: " + ex.StackTrace);
            }
            return 0;
        }


        //+------------------------------------------------------------------+
        //| AUTO GENERATED CODE. THIS METHODS USED FOR INDICATOR CACHING     |
        //+------------------------------------------------------------------+
        #region Auto Generated Code

        [Description("Parameters order Symbol, TimeFrame")]
        public override bool IsSameParameters(params object[] values)
        {
            if (values.Length != 4)
                return false;
            if (!CompareString(Symbol, (string)values[0]))
                return false;
            if (TimeFrame != (int)values[1])
                return false;
            if (Period != (int)values[2])
                return false;
            if (PriceType != (PriceTypes)values[3])
                return false;
            return true;
        }

        [Description("Parameters order Symbol, TimeFrame")]
        public override void SetIndicatorParameters(params object[] values)
        {
            if (values.Length != 6)
                throw new ArgumentException("Invalid parameters number");
            Symbol = (string)values[0];
            TimeFrame = (int)values[1];
            Period = (int)values[2];
            PriceType = (PriceTypes)values[3];
        }
        #endregion

        // WillamsPctR Class Object
        internal class WillamsPctRobj
        {
            internal int Period;
            public bool overBought;
            public bool overSold;
            public bool neutral;
            public double value;
            public int state;
            public int prevState;
            public int trendDir;
            internal double prevValue;
            internal bool firstrun;
            double prevPrice;
            WilliamsPctR ea;
            Queue<double> theQ;
            double highest;
            double lowest;

            // WillamsPctRobj constructor
            WillamsPctRobj()
            {
                Period = 14;
                overBought = false;
                overSold = false;
                neutral = false;
                state = 0;
                trendDir = 0;
                prevState = state;
                value = double.MinValue;
                prevValue = value;
                prevPrice = 0;
                theQ = null;
                highest = double.MinValue;
                lowest = double.MinValue;
            }

            // WillamsPctRobj constructor with input parameters
            internal WillamsPctRobj(WilliamsPctR ea, int period) : this()   // do WilliamsPctR ea;
            {
                this.ea = ea;
                Period = period;
                firstrun = true;
                theQ = new Queue<double>();
                highest = double.MinValue;
                lowest = double.MinValue;

            }

            internal void Init(double thePrice)  // Initialize Indicator
            {
                value = 0;
                prevValue = value;
                overBought = false;
                overSold = false;
                neutral = false;
                firstrun = false;
                state = 0;
                trendDir = 0;
                prevState = state;
                prevPrice = thePrice;
                theQ = new Queue<double>();
                highest = double.MinValue;
                lowest = double.MinValue;
            }

            internal double Calc(double thePrice)  // Calculate Indicator values
            {
                // Williams Pct R = (Highest High - Close) / (Highest High - Lowest Low)
                if (Period < 2)
                    throw new Exception("WilliamsPctR: period < 2, invalid !!");
                if (firstrun)
                {
                    Init(thePrice);
                }
                prevState = state;
                prevValue = value;
                value = -50;
                theQ.Enqueue(thePrice);
                while (theQ.Count > Period)
                    theQ.Dequeue();
                highest = theQ.Max();
                lowest = theQ.Min();
                if ((highest - lowest) > 0 && theQ.Count > 1)
                {
                    value = -100 * (highest - thePrice) / (highest - lowest);
                }
                overBought = (value >= -20);
                overSold = (value <= -80);
                neutral = (value < 20) && (value < -80);
                state = overBought ? 1 : overSold ? -1 : state;
                if (state != prevState)
                    trendDir = state;
                prevPrice = thePrice;
                return value;
            }
        }

        /// <summary>  
        ///  UpdateBuffers - update Alveo Indicator buffers with new data
        /// <param name="entry">index value into buffer.</param>
        /// </summary>  
        internal void UpdateBuffers(int entry)
        {
            var value = pctR.value;
            var indx = e - entry - 1;               // put data into buffers in the reverse order, for Alveo.
            PctR[indx] = value;            // Initialize with EMPTY_VALUE
            UpperLine[indx] = -20;
            LowerLine[indx] = -80;
        }

        static double GetThePrice(int type, Bar b)
        {
            double price = -1;
            switch (type)
            {
                case (int)PriceTypes.PRICE_CLOSE:
                    price = (double)b.Close;
                    break;
                case (int)PriceTypes.PRICE_OPEN:
                    price = (double)b.Open;
                    break;
                case (int)PriceTypes.PRICE_HIGH:
                    price = (double)b.High;
                    break;
                case (int)PriceTypes.PRICE_LOW:
                    price = (double)b.Low;
                    break;
                case (int)PriceTypes.PRICE_MEDIAN:
                    price = Math.Round((double)(b.High + b.Low) / 2, 5);
                    break;
                case (int)PriceTypes.PRICE_TYPICAL:
                    price = Math.Round((double)(b.High + b.Low + b.Close) / 3, 5);
                    break;
                case (int)PriceTypes.PRICE_WEIGHTED:
                    price = Math.Round((double)(b.High + b.Low + 2 * b.Close) / 4, 5);
                    break;
                case (int)PriceTypes.PRICE_OHLC:
                    price = Math.Round((double)(b.Open + b.High + b.Low + b.Close) / 4, 5);
                    break;
                case (int)PriceTypes.PRICE_P7:
                    price = Math.Round((double)(b.Open + b.High + 2 * b.Low + 3 * b.Close) / 7, 5);
                    break;
            }
            return price;
        }
    }
}
