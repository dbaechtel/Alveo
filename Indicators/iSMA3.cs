/*
 * * LEGAL DISCLAIMER *

The currency markets can do ANYTHING at ANY TIME.
No one can guarantee or forecast how these results will perform or behave in future markets.
Anyone who uses this product or this information is responsible for deciding If, Where, When and How this product and information are used.
Anyone who uses this product or this information is responsible and liable for any outcomes that might result from the use of this product or this information.
There is no warranty or guarantee provided or implied for this product or this information for any purpose.
 */

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using Alveo.Interfaces.UserCode;
using Alveo.UserCode;
using Alveo.Common;
using Alveo.Common.Classes;

namespace Alveo.UserCode
{
    [Serializable]
    [Description("")]
    public class iSMA3 : IndicatorBase
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
            PRICE_OHLC = 7
        }

        #region Properties
        [Category("Settings")]
        [Description("Period in Bars [ex: 5]")]
        public int MA_period { get; set; }

        [Category("Settings")]
        [Description("Threshold 1e-6")]
        public int Threshold { get; set; }

        [Description("Price type on witch Stochastic will be calculated")]
        [Category("Settings")]
        [DisplayName("Price Type")]
        public PriceTypes PriceType { get; set; }
        #endregion

        bool simulate = false;
        int timeFrame;
        SMAobj sma;
        Array<double> UpTrend;              // upwards trend
        Array<double> DownTrend;            // Downtrend trend
        Array<double> Mixed;                // Mixed trend
        iSMA3 parent;
        Bar b;              // holds latest chart Bar data from Alveo
        Bar prevBar;        // holds latest chart Bar data from Alveo
        bool firstrun;
        int counted_bars;
        int e;              // number of bars for the indicator to calculate

        public iSMA3()
        {
            indicator_buffers = 3;
            indicator_chart_window = true;
            MA_period = 50;
            Threshold = 0;
            PriceType = PriceTypes.PRICE_OHLC;
            indicator_width1 = 2;               // width of line 1 on the chart
            indicator_color1 = Colors.Green;     // line color
            parent = this;
            UpTrend = new Array<double>();
            DownTrend = new Array<double>();
            Mixed = new Array<double>();
            firstrun = true;
            simulate = false;
            counted_bars = 0;
            copyright = "";
            link = "";
        }

        //+------------------------------------------------------------------+");
        //| Custom indicator initialization function                         |");
        //+------------------------------------------------------------------+");
        protected override int Init()
        {
            Print("SMA.Init: started. ");
            try  // to catch and handle Exceptions that might occur in this code block
            {
                timeFrame = (int)Chart.TimeFrame;
                IndicatorBuffers(indicator_buffers);        // Allocates memory for buffers used for custom indicator calculations.
                SetIndexBuffer(0, UpTrend);                 // binds a specified indicator buffer with one-dimensional dynamic array of the type double.
                //SetIndexArrow(0, 159);                      // Sets an arrow symbol for indicators line of the DRAW_ARROW type. 159=dot.
                SetIndexStyle(0, DRAW_LINE, STYLE_SOLID);       // Sets the shape, style, width and color for the indicator line.
                SetIndexLabel(0, "Uptrend");   // Sets description for showing in the DataWindow and in the tooltip on Chart.
                SetIndexBuffer(1, DownTrend);                 // binds a specified indicator buffer with one-dimensional dynamic array of the type double.
                //SetIndexArrow(0, 159);                      // Sets an arrow symbol for indicators line of the DRAW_ARROW type. 159=dot.
                SetIndexStyle(1, DRAW_LINE, STYLE_SOLID);       // Sets the shape, style, width and color for the indicator line.
                SetIndexLabel(1, "DownTrend");   // Sets description for showing in the DataWindow and in the tooltip on Chart.
                SetIndexBuffer(2, Mixed);                 // binds a specified indicator buffer with one-dimensional dynamic array of the type double.
                //SetIndexArrow(0, 159);                      // Sets an arrow symbol for indicators line of the DRAW_ARROW type. 159=dot.
                SetIndexStyle(2, DRAW_LINE, STYLE_SOLID);       // Sets the shape, style, width and color for the indicator line.
                SetIndexLabel(2, "Mixed");   // Sets description for showing in the DataWindow and in the tooltip on Chart.
                indicator_width1 = 2;               // width of line 1 on the chart
                indicator_width2 = 2;
                indicator_width3 = 2;
                indicator_color1 = Colors.Blue;     // line colors
                indicator_color2 = Colors.Red;
                indicator_color3 = Colors.Green;
                IndicatorShortName("iSMA3 v1.0 (" + MA_period + ")");
                sma = new SMAobj(ref parent, MA_period, Threshold);
                counted_bars = 0;
                //Print("SMA: Init: finished. ");
            }
            catch (Exception ex)    // catch and Print any exceptions that may have happened
            {
                Print("SMA: Init: Exception: " + ex.Message);
                Print("SMA: " + ex.StackTrace);
            }
            return 0;
        }

        //+------------------------------------------------------------------+");
        //| Custom indicator deinitialization function                       |");
        //+------------------------------------------------------------------+");
        protected override int Deinit()
        {
            return 0;
        }

        //+------------------------------------------------------------------+");
        //| Custom indicator iteration function                              |");
        //+------------------------------------------------------------------+");
        protected override int Start()
        {
            //Print("SMA: Start: started.");
            try  // to catch and handle Exceptions that might occur in this code block
            {
                e = Bars - 1;   // e = largest index in ChartBars array
                if (e < 2)      // not enough data
                    return -1;
                prevBar = b;
                if (firstrun)   // on first run, calculate EMA on all ChartBars data
                {
                    b = ChartBars[e - 1];           // b = refernec to oldest ChartBars data
                    prevBar = b;
                    firstrun = false;               // firstrun initialization completed 
                }
                else // not firstrun; only calculate EMA on ChartBars not already processed
                {
                    if (counted_bars > e)
                        counted_bars = 0;
                    e -= counted_bars;          // reduce e count by bars previously processed
                }
                if (e < 2)                      // no data to process
                    return 0;                   // we're out of here, for now    
                for (int i = 0; i < e - 1; i++)     // iterate each bar to process ; don't do ChartBars[0]
                {
                    var indx = e - i - 1;
                    b = ChartBars[indx];                            // get oldest chart bar in array
                    counted_bars++;
                    if (prevBar == null)
                        prevBar = b;
                    prevBar = b;
                    var barData = new BarData(b);
                    sma.Calc(barData);                              // calculate new EMA value
                    if (sma.value > 0)
                        UpdateBuffers(i);                     // update Alvelo Indicator buffers
                }
            }
            catch (Exception ex)    // catch and Print any exceptions that may have happened
            {
                Print("SMA: Start: Exception: " + ex.Message);
                Print("SMA: " + ex.StackTrace);
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
            if (MA_period != (int)values[2])
                return false;
            if (Threshold != (int)values[3])
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
            MA_period = (int)values[2];
            Threshold = (int)values[3];
            PriceType = (PriceTypes)values[4];
        }
        #endregion

        // encapsulated BarData Class
        // holds data needed for each chart Bar
        public class BarData
        {
            internal string symbol;
            internal Bar bar;
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
            internal double HEMA;
            internal double smoothedK;
            internal double smoothedD;
            internal double slowedD;
            internal double spread;
            internal DayOfWeek wd;
            internal TimeSpan tod;

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
                bar = ibar;
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
                BarTime = DateTime.MinValue;
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
                HEMA = 0;
                smoothedK = 50;
                smoothedD = 50;
                slowedD = 50;
                pctChange = 0;
                spread = 0;
                wd = BarTime.DayOfWeek;
                tod = BarTime.TimeOfDay;
            }
        }

        #region SMAobj Class
        internal class SMAobj
        {
            iSMA3 ea;
            int Period;
            double Threshold;
            Queue<double> Q;
            internal bool isRrising;
            internal bool isFalling;
            internal int trendDir;
            internal int prevTrendDir;
            internal int prevState;
            internal bool trendChanged;
            internal bool stateChanged;
            internal double prevValue;
            internal double value;
            internal bool firstrun;
            DateTime prevTM;

            // SMAobj constructor
            internal SMAobj()
            {
                Period = 50;
                Threshold = 1 * 1e-6;
                Q = null;
                isRrising = false;
                isFalling = false;
                trendDir = 0;
                prevTrendDir = 0;
                prevState = 0;
                trendChanged = false;
                stateChanged = false;
                value = double.MinValue;
                prevValue = value;
                prevTM = DateTime.MinValue;
                ea = null;
            }

            // SMAobj constructor with input parameters
            internal SMAobj(ref iSMA3 ea, int period, int threshold = 0) : this()   // do SMAobj() first
            {
                this.ea = ea;
                Period = period;
                Q = new Queue<double>();
                Threshold = (double)threshold * 1e-6;
                firstrun = true;
            }

            internal void Init(BarData b)  // Initialize Indicator
            {
                Q.Clear();
                value = double.MinValue;
                prevValue = value;
                isRrising = false;
                isFalling = false;
                trendDir = 0;
                prevState = 0;
                prevTrendDir = 0;
                trendChanged = false;
                stateChanged = false;
                firstrun = false;
                prevTM = b.BarTime;
            }

            internal double Calc(BarData b)  // Calculate Indicator values
            {
                if (Period < 1)
                    throw new Exception("SMA.Calc: period < 1, invalid !!");
                if (Threshold < 0)
                    throw new Exception("SMA.Calc: Threshold < 0, invalid !!");
                if (firstrun)
                {
                    Init(b);
                }
                if (false && !ea.simulate && prevTM.Year > 1980)
                {
                    var dur = b.BarTime.Subtract(prevTM);
                    if (dur > TimeSpan.FromSeconds(0) && dur > TimeSpan.FromMinutes((int)ea.timeFrame * 2))
                    {
                        ea.Print("SMA: must reload. dur=" + dur.ToString() + " tm=" + b.BarTime.ToString() + " prevTM=" + prevTM.ToString());
                        Init(b);
                        ea.counted_bars = 0;
                        return value;
                    }
                }
                prevTM = b.BarTime;
                if (trendDir != 0)
                    prevTrendDir = trendDir;
                prevState = trendDir;
                double thePrice = ea.GetThePrice((int)ea.PriceType, b);
                Q.Enqueue(thePrice);
                while (Q.Count > Period)
                    Q.Dequeue();
                prevValue = value;
                value = Q.Average();
                if (prevValue < 0)
                    prevValue = value;
                isRrising = ((value - prevValue) > Threshold);
                isFalling = ((prevValue - value) > Threshold);
                trendDir = isRrising ? 1 : (isFalling ? -1 : 0);
                trendChanged = (trendDir * prevTrendDir < 0);
                stateChanged = (trendDir != prevState);
                return value;
            }
        }
        #endregion

        public double GetThePrice(int type, BarData b)
        {
            double price = -1;
            switch (type)
            {
                case (int)PriceTypes.PRICE_CLOSE:
                    price = b.close;
                    break;
                case (int)PriceTypes.PRICE_OPEN:
                    price = b.open;
                    break;
                case (int)PriceTypes.PRICE_HIGH:
                    price = b.high;
                    break;
                case (int)PriceTypes.PRICE_LOW:
                    price = b.low;
                    break;
                case (int)PriceTypes.PRICE_MEDIAN:
                    price = (b.high + b.low) / 2;
                    break;
                case (int)PriceTypes.PRICE_TYPICAL:
                    price = (b.high + b.low + b.close) / 3;
                    break;
                case (int)PriceTypes.PRICE_WEIGHTED:
                    price = (b.high + b.low + 2 * b.close) / 4;
                    break;
                case (int)PriceTypes.PRICE_OHLC:
                    price = Math.Round((b.open + b.high + b.low + b.close) / 4, 5);
                    break;
            }
            return price;
        }

        /// <summary>  
        ///  UpdateBuffers - update Alveo Indicator buffers with new data
        /// <param name="entry">index value into buffer.</param>
        /// </summary>  
        internal void UpdateBuffers(int entry)
        {
            var value = sma.value;
            var indx = e - entry - 1;               // put data into buffers in the reverse order, for Alveo.
            UpTrend[indx] = EMPTY_VALUE;            // Initialize with EMPTY_VALUE
            DownTrend[indx] = EMPTY_VALUE;
            Mixed[indx] = EMPTY_VALUE;
            if (sma.isRrising)   // if UpTrend
            {
                UpTrend[indx] = value;           // UpTrend buffer gets HEMAvalue
            }
            else if (sma.isFalling) // Downtrend
            {
                DownTrend[indx] = value;         // DownTrend buffer gets HEMAvalue
            }
            else  // otherwise
            {
                Mixed[indx] = value;     // Consolidation buffer gets HEMAvalue
            }
            if (sma.stateChanged)                  // if trendDir changed from previous call
            {
                switch (sma.prevState)          // place connecting HEMAvalue into proper buffer to connect the lines
                {
                    case 1: // uptrend
                        UpTrend[indx] = value;
                        break;
                    case -1: // downtrend
                        DownTrend[indx] = value;
                        break;
                    case 0:  // consolidation
                        Mixed[indx] = value;
                        break;
                }
            }
        }

    }
}