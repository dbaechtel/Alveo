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
    public class HMA3 : IndicatorBase
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
        Array<double> UpTrend;
        Array<double> DownTrend;
        Array<double> Consolidation;
        HMAobj hma;
        int e;
        Bar b;              // holds latest chart Bar data from Alveo
        Bar prevBar;        // holds latest chart Bar data from Alveo
        bool firstrun;
        #endregion

        public HMA3()
        {
            // Basic indicator initialization. Don't use this constructor to calculate values
            period = 21;
            indicator_chart_window = true;
            indicator_buffers = 3;
            indicator_width1 = 2;
            indicator_width2 = 2;
            indicator_width3 = 2;
            indicator_color1 = Colors.Blue;
            indicator_color2 = Colors.Red;
            indicator_color3 = Colors.LimeGreen;
            UpTrend = new Array<double>();
            DownTrend = new Array<double>();
            Consolidation = new Array<double>();
            copyright = "(C)2018 Entity3 LLC";
            link = "";
            hma = null;
        }

        [Category("Settings")]
        [DisplayName("MA Period")]
        public int period { get; set; }

        /// <param name="slopeThreshold">Specifies at what slope Uptrend and Downtrend are determined.</param>
        [Category("Settings")]
        [DisplayName("Slope Trheshold * 1e-6. ")]
        [Description("Specifies at what slope Uptrend and Downtrend are determined. [ex: 10]")]
        public int SlopeThreshold { get; set; }

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
                SetIndexBuffer(0, UpTrend);
                SetIndexArrow(0, 159);
                SetIndexBuffer(1, DownTrend);
                SetIndexArrow(1, 159);
                SetIndexBuffer(2, Consolidation);
                SetIndexArrow(2, 159);
                SetIndexStyle(0, DRAW_LINE, STYLE_SOLID);
                SetIndexLabel(0, "HMA3(" + period + ").Bull");
                SetIndexStyle(1, DRAW_LINE, STYLE_SOLID);
                SetIndexLabel(1, "Bear");
                SetIndexStyle(2, DRAW_LINE, STYLE_SOLID);
                SetIndexLabel(2, "Mixed");
                IndicatorShortName("Hull Moving Average(" + period + ")");
                hma = new HMAobj(this, period, SlopeThreshold);
                b = null;
                prevBar = null;
            }
            catch (Exception ex)    // catch and Print any exceptions that may have happened
            {
                Print("HMA3: Start: Exception: " + ex.Message);
                Print("HMA3: " + ex.StackTrace);
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
                if (firstrun)   // on first run, calculate HEMA on all ChartBars data
                {
                    b = ChartBars[e - 1];           // b = refernec to oldest ChartBars data
                    prevBar = b;
                    firstrun = false;               // firstrun initialization completed 
                }
                else // not firstrun; only calculate HEMA on ChartBars not already processed
                {
                    var counted_bars = IndicatorCounted();
                    if (counted_bars < 0)
                        throw new Exception("CFit3: Start: invalid IndicatorCounted value.");            // invalid value
                    e -= counted_bars;          // reduce e count by bars previously processed
                }

                if (e < 1)                      // no data to process
                    return 0;                   // we're out of here, for now    

                for (int i = 0; i < e; i++)     // iterate each bar to process  
                {
                    b = ChartBars[e - i - 1];                                   // get oldest chart bar in array
                    if (prevBar == null)
                        prevBar = b;
                    double thePrice = GetThePrice((int)PriceType, b);
                    hma.Calc(thePrice);
                    //Print("Period=" + hma.Period + " thePrice=" + thePrice + " value = " + hma.value + " wma1.value=" + hma.wma1.value + " wma2.value=" + hma.wma2.value + " term3=" + hma.term3);
                    UpdateBuffers(i);                     // update Alvelo Indicator buffers
                }
            }
            catch (Exception ex)    // catch and Print any exceptions that may have happened
            {
                Print("HMA3: Start: Exception: " + ex.Message);
                Print("HMA3: " + ex.StackTrace);
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
            if (values.Length != 5)
                return false;
            if (!CompareString(Symbol, (string)values[0]))
                return false;
            if (TimeFrame != (int)values[1])
                return false;
            if (period != (int)values[2])
                return false;
            if (SlopeThreshold != (int)values[3])
                return false;
            if (PriceType != (PriceTypes)values[4])
                return false;
            return true;
        }

        [Description("Parameters order Symbol, TimeFrame")]
        public override void SetIndicatorParameters(params object[] values)
        {
            if (values.Length != 5)
                throw new ArgumentException("Invalid parameters number");
            Symbol = (string)values[0];
            TimeFrame = (int)values[1];
            period = (int)values[2];
            SlopeThreshold = (int)values[3];
            PriceType = (PriceTypes)values[4];
        }
        #endregion

        // HEMA Class Object
        internal class HMAobj
        {
            internal WMAobj wma1;
            internal WMAobj wma2;
            internal WMAobj wma3;
            internal int Period;
            double Threshold;
            int sqrtPeriod;
            internal bool isRrising;
            internal bool isFalling;
            internal int trendDir;
            internal int prevTrendDir;
            internal bool trendChanged;
            internal double value;
            internal double prevValue;
            internal bool firstrun;
            internal double term3;
            HMA3 ea;

            // HMAobj constructor
            HMAobj()
            {
                Period = 25;
                Threshold = 0;
                sqrtPeriod = (int)Math.Round(Math.Sqrt(Period));
                isRrising = false;
                isFalling = false;
                trendDir = 0;
                prevTrendDir = 0;
                trendChanged = false;
                value = double.MinValue;
                prevValue = value;
            }

            // HMAobj constructor with input parameters
            internal HMAobj(HMA3 ea, int period, int threshold) : this()   // do HEMA() first
            {
                this.ea = ea;
                Period = period;
                Threshold = (double)threshold * 1e-6;
                sqrtPeriod = (int)Math.Round(Math.Sqrt((double)Period));
                wma1 = new WMAobj(period, 0);
                wma2 = new WMAobj((int)Math.Round(((double)period + 1) / 2), 0);
                wma3 = new WMAobj(sqrtPeriod, threshold);
                firstrun = true;
            }

            internal void Init(double thePrice)  // Initialize Indicator
            {
                wma1.Init(thePrice);
                wma2.Init(thePrice);
                wma3.Init(thePrice);
                value = wma3.value;
                prevValue = value;
                isRrising = false;
                isFalling = false;
                trendDir = 0;
                prevTrendDir = 0;
                trendChanged = false;
                firstrun = false;
                term3 = 0;
            }

            internal double Calc(double thePrice)  // Calculate Indicator values
            {
                // HMA(n) = WMA(2 * WMA(n / 2) – WMA(n)), sqrt(n))
                if (Period < 2)
                    throw new Exception("HMAcalc: period < 2, invalid !!");
                if (Threshold < 0)
                    throw new Exception("HMAcalc: Threshold < 0, invalid !!");
                if (firstrun)
                {
                    Init(thePrice);
                }
                prevTrendDir = trendDir;
                prevValue = value;
                wma1.Calc(thePrice);
                wma2.Calc(thePrice);
                term3 = 2 * wma2.value - wma1.value;
                value = wma3.Calc(term3);
                isRrising = wma3.isRrising;
                isFalling = wma3.isFalling;
                trendDir = isRrising ? 1 : (isFalling ? -1 : 0);
                trendChanged = (trendDir * prevTrendDir < 0);
                return value;
            }
        }

        internal class WMAobj
        {
            internal int Period;
            double Threshold;
            internal double lastPrice;
            internal double prevValue;
            internal double value;
            internal bool isRrising;
            internal bool isFalling;
            internal int trendDir;
            internal int prevTrendDir;
            internal bool trendChanged;
            internal bool stateChanged;
            internal Queue<double> Q;
            internal int cnt2;

            WMAobj()
            {
                Period = 1;
                Threshold = 0;
                value = 0;
                lastPrice = double.MinValue;
                Q = new Queue<double>();
            }

            internal WMAobj(int period, int threshold = 0) : this()
            {
                Period = period;
                Threshold = (double)threshold * 1e-6;
            }

            internal void Init(double price)
            {
                value = price;
                prevValue = value;
                Q.Clear();
            }

            internal double Calc(double thePrice)
            {
                prevValue = value;
                value = 0;
                lastPrice = thePrice;
                Q.Enqueue(thePrice);
                while (Q.Count > Period)
                    Q.Dequeue();
                var arr = Q.ToArray();
                cnt2 = arr.Length;
                double sum = 0;
                double mult = 0;
                for (int i = 0; i < cnt2; i++)
                {
                    mult += 1;
                    value += mult * arr[i];
                    sum += mult;
                }
                if (sum > 0)
                    value /= sum;
                else
                    value = double.MinValue;
                var diff = value - prevValue;
                isRrising = (diff > Threshold);
                isFalling = (diff < -Threshold);
                prevTrendDir = trendDir;
                trendDir = isRrising ? 1 : (isFalling ? -1 : 0);
                trendChanged = (trendDir * prevTrendDir < 0);
                return value;
            }
        }

        /// <summary>  
        ///  UpdateBuffers - update Alveo Indicator buffers with new data
        /// <param name="entry">index value into buffer.</param>
        /// </summary>  
        internal void UpdateBuffers(int entry)
        {
            var value = hma.value;
            var indx = e - entry - 1;               // put data into buffers in the reverse order, for Alveo.
            UpTrend[indx] = EMPTY_VALUE;            // Initialize with EMPTY_VALUE
            DownTrend[indx] = EMPTY_VALUE;
            Consolidation[indx] = EMPTY_VALUE;
            if (hma.isRrising)   // if UpTrend
            {
                UpTrend[indx] = value;           // UpTrend buffer gets HEMAvalue
            }
            else if (hma.isFalling) // Downtrend
            {
                DownTrend[indx] = value;         // DownTrend buffer gets HEMAvalue
            }
            else  // otherwise
            {
                Consolidation[indx] = value;     // Consolidation buffer gets HEMAvalue
            }
            if (hma.prevTrendDir != hma.trendDir)                  // if trendDir changed from previous call
            {
                //Print("trendChanged=" + cf.trendChanged + " prevTrendDir=" + cf.prevTrendDir);
                switch (hma.trendDir)          // place connecting HEMAvalue into proper buffer to connect the lines
                {
                    case 1: // uptrend
                        UpTrend[indx + 1] = hma.prevValue;
                        break;
                    case -1: // downtrend
                        DownTrend[indx + 1] = hma.prevValue;
                        break;
                    case 0:  // consolidation
                        Consolidation[indx + 1] = hma.prevValue;
                        break;
                }
            }
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
