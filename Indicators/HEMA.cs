using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows.Media;
using Alveo.Interfaces.UserCode;
using Alveo.Common.Classes;

namespace Alveo.UserCode
{

    // As usual,
    // The currency markets can do ANYTHING at ANYTIME.
    // No warranty is provided for this product and no suitability is implied for any use.
    // There are no protections included in this code that can limit the outcomes from its use.
    // The user is solely responsible for determining when, where and how to use this code.
    // By using this product, the user accepts full liability related to the use of this porduct and the outcome for doing so.

    /// <summary>  
    ///  The HEMA class formulates the HEMA Indicator on an Alveo chart.  
    ///    
    ///  The period parameter set the strength of the filtering by the HEMA.  
    ///  The slopeThreshold parameter specifies at what slope Uptrend and Downtrend are determined.  
    ///  The slopeThreshold parameter allows the user to decide which slope value signals a strong Uptrend or Downtrend.  
    ///  
    ///  This Indicator calculates three lines on the chart:.  
    ///    * Uptrend in Blue
    ///    * Downtrend in Red
    ///    * Consolidation in Green
    /// 
    /// </summary>  

    [Serializable]
    [Description("Alveo HEMA Indicator v1.2")]
    public class HEMA : IndicatorBase
    {
        #region Properties

        // User settable Properties for this Alveo Indicator
        /// <param name="period">Sets the strength of the filtering.</param>
        [Category("Settings")]
        [DisplayName("MA Period. ")]
        [Description("Sets the strength of the filtering. [ex: 20]")]
        public int IndPeriod { get; set; }

        /// <param name="slopeThreshold">Specifies at what slope Uptrend and Downtrend are determined.</param>
        [Category("Settings")]
        [DisplayName("Slope Trheshold * 1e-6. ")]
        [Description("Specifies at what slope Uptrend and Downtrend are determined. [ex: 10]")]
        public int SlopeThreshold { get; set; }
        #endregion

        //Buffers for Alveo HEMA Indicator
        Array<double> UpTrend;          // upwards trend
        Array<double> DownTrend;        // downwards trend
        Array<double> Consolidation;    // in between UpTrend and DownTrend

        Bar b;              // holds latest chart Bar data from Alveo
        Bar prevBar;        // holds latest chart Bar data from Alveo
        int counted_bars;   // amount of bars of bars on chart already processed by the indicator.
        int e;              // number of bars for the indicator to calculate
        double thePrice;    // holds the currency pair price for the NDI calculation. In units of the bas currency.
        bool firstrun;      // firstrun = true on first execution of the Start function. False otherwise.
        HEMAobj hema;

        /// <summary>  
        ///  C# constructor for HEMA Class
        ///  called to initialize the class when class is created by Alveo
        /// </summary>  
        public HEMA()
        {
            try
            {
                // Basic indicator initialization. Don't use this constructor to calculate values

                indicator_buffers = 3;              // 3 lines on Alveo chart require 3 buffers
                indicator_chart_window = true;
                firstrun = true;                    // initially set true for first run
                b = null;
                prevBar = null;

                IndPeriod = 31;                     // Initial value for HEMA period
                SlopeThreshold = 10;                // Initial value for slopeThreshold

                indicator_width1 = 2;               // width of line 1 on the chart
                indicator_width2 = 2;
                indicator_width3 = 2;
                indicator_color1 = Colors.Blue;     // line colors
                indicator_color2 = Colors.Red;
                indicator_color3 = Colors.Green;

                UpTrend = new Array<double>();      // 3 data buffers for Alveo Indicator
                DownTrend = new Array<double>();
                Consolidation = new Array<double>();

                copyright = "";
                link = "";
            }
            catch (Exception e)
            {
                string msg = e.Message;
            }
        }

        //+------------------------------------------------------------------+");
        //| Custom indicator initialization function                         |");
        //| Called by Alveo to initialize the HEMA Indicator at startup.      |");
        //+------------------------------------------------------------------+");
        protected override int Init()
        {
            try
            {
                // ENTER YOUR CODE HERE
                IndicatorBuffers(indicator_buffers);        // Allocates memory for buffers used for custom indicator calculations.
                SetIndexBuffer(0, UpTrend);                 // binds a specified indicator buffer with one-dimensional dynamic array of the type double.
                SetIndexArrow(0, 159);                      // Sets an arrow symbol for indicators line of the DRAW_ARROW type. 159=dot.
                SetIndexBuffer(1, DownTrend);               // repeat for each buffer
                SetIndexArrow(1, 159);
                SetIndexBuffer(2, Consolidation);
                SetIndexArrow(2, 159);

                SetIndexStyle(0, DRAW_LINE, STYLE_SOLID);       // Sets the shape, style, width and color for the indicator line.
                SetIndexLabel(0, "HEMA(" + IndPeriod + ").Bull");   // Sets description for showing in the DataWindow and in the tooltip on Chart.
                SetIndexStyle(1, DRAW_LINE, STYLE_SOLID);       // repeat for all 3 buffers
                SetIndexLabel(1, "HEMA(" + IndPeriod + ").Bear");
                SetIndexStyle(2, DRAW_LINE, STYLE_SOLID);
                SetIndexLabel(2, "HEMA(" + IndPeriod + ").Mixed");

                // Sets the "short" name of a custom indicator to be shown in the DataWindow and in the chart subwindow.
                IndicatorShortName("HEMA Indicator v2.0 (" + IndPeriod + "," + SlopeThreshold + ")");

                hema = new HEMAobj(IndPeriod, SlopeThreshold);
                thePrice = double.MinValue;

                Print("HEMA: Started. [" + Chart.Symbol + "] period=" + Period());      // Print this message to Alveo Log file on startup
            }
            catch (Exception ex)
            {
                Print("HEMA: Init: Exception: " + ex.Message);
                Print("HEMA: " + ex.StackTrace);
            }
            return 0;   // done
        }

        //+------------------------------------------------------------------+");
        //| Custom indicator deinitialization function                       |");
        //| Called by Alveo when the Indicator is closed                     |");
        //+------------------------------------------------------------------+");
        protected override int Deinit()
        {
            // ENTER YOUR CODE HERE
            return 0;
        }

        //+--------------------------------------------------------------------------+");
        //| Custom indicator iteration function                                      |");
        //| Called by Alveo everytime a new chart bar appears, and maybe more often  |");
        //+--------------------------------------------------------------------------+");
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
                    thePrice = (double)(b.Open + b.Close) / 2.0;     // initialize hema to thePrice of the oldest bar
                    hema.Init(thePrice);
                    firstrun = false;               // firstrun initialization completed 
                }
                else // not firstrun; only calculate HEMA on ChartBars not already processed
                {
                    counted_bars = IndicatorCounted();
                    if (counted_bars < 0)
                        throw new Exception("HEMA: Start: invalid IndicatorCounted value.");            // invalid value
                    e -= counted_bars;          // reduce e count by bars previously processed
                }

                if (e < 1)                      // no data to process
                    return 0;                   // we're out of here, for now    

                for (int i = 0; i < e; i++)     // iterate each bar to process  
                {
                    b = ChartBars[e - i - 1];                                   // get oldest chart bar in array
                    if (prevBar == null)
                        prevBar = b;
                    thePrice = (double)(b.Open + b.Close) / 2.0;
                    var gap = Math.Abs((double)(prevBar.Close - b.Open));
                    prevBar = b;
                    if (gap > 50 * Point)
                    {
                        Print("HEMA: Gap detected. gap=" + gap / Point + " points. " + b.BarTime.ToString());
                        hema.Init(thePrice);
                    }
                    hema.Calc(thePrice);                                        // calculate new HEMA value
                    UpdateBuffers(i);                     // update Alvelo Indicator buffers
                }
            }
            catch (Exception ex)    // catch and Print any exceptions that may have happened
            {
                Print("HEMA: Start: Exception: " + ex.Message);
                Print("HEMA: " + ex.StackTrace);
            }
            return 0;
        }

        /// <summary>  
        ///  UpdateBuffers - update Alveo Indicator buffers with new data
        /// <param name="entry">index value into buffer.</param>
        /// </summary>  
        internal void UpdateBuffers(int entry)
        {
            var HEMAvalue = hema.HEMAval;
            var indx = e - entry - 1;               // put data into buffers in the reverse order, for Alveo.
            UpTrend[indx] = EMPTY_VALUE;            // Initialize with EMPTY_VALUE
            DownTrend[indx] = EMPTY_VALUE;
            Consolidation[indx] = EMPTY_VALUE;
            if (hema.isRrising)   // if UpTrend
            {
                UpTrend[indx] = HEMAvalue;           // UpTrend buffer gets HEMAvalue
            }
            else if (hema.isFalling) // Downtrend
            {
                DownTrend[indx] = HEMAvalue;         // DownTrend buffer gets HEMAvalue
            }
            else  // otherwise
            {
                Consolidation[indx] = HEMAvalue;     // Consolidation buffer gets HEMAvalue
            }
            if (hema.stateChanged)                  // if trendDir changed from previous call
            {
                switch (hema.prevState)          // place connecting HEMAvalue into proper buffer to connect the lines
                {
                    case 1: // uptrend
                        UpTrend[indx] = HEMAvalue;
                        break;
                    case -1: // downtrend
                        DownTrend[indx] = HEMAvalue;
                        break;
                    case 0:  // consolidation
                        Consolidation[indx] = HEMAvalue;
                        break;
                }
            }
        }

        //+------------------------------------------------------------------+
        //| AUTO GENERATED CODE. THIS METHODS USED FOR INDICATOR CACHING     |
        //+------------------------------------------------------------------+
        #region Auto Generated Code

        [Description("Parameters order Symbol, TimeFrame")]
        public override bool IsSameParameters(params object[] values)  // determine if Indicator parameter values have not changed.
        {
            if (values.Length != 4)
                return false;

            if (!CompareString(Symbol, (string)values[0]))
                return false;

            if (TimeFrame != (int)values[1])
                return false;

            if (IndPeriod != (int)values[2])
                return false;

            if (SlopeThreshold != (double)values[3])
                return false;

            return true;
        }

        [Description("Parameters order Symbol, TimeFrame")]
        public override void SetIndicatorParameters(params object[] values)     // Set Indicator values from cache
        {
            if (values.Length != 4)
                throw new ArgumentException("Invalid parameters number");

            Symbol = (string)values[0];
            TimeFrame = (int)values[1];
            IndPeriod = (int)values[2];
            SlopeThreshold = (int)values[3];
        }

        #endregion  // Auto Generated Code

        internal class HEMAobj
        {
            EMAobj ema1;
            EMAobj ema2;
            EMAobj ema3;
            double Period;
            double Threshold;
            double sqrtPeriod;
            internal bool isRrising;
            internal bool isFalling;
            internal int trendDir;
            internal int prevTrendDir;
            internal int prevState;
            internal bool trendChanged;
            internal bool stateChanged;
            internal double HEMAval;

            HEMAobj()
            {
                Period = 12;
                Threshold = 1 * 1e-6;
                sqrtPeriod = 1;
                isRrising = false;
                isFalling = false;
                trendDir = 0;
                prevTrendDir = 0;
                prevState = 0;
                trendChanged = false;
                stateChanged = false;
                HEMAval = double.MinValue;
            }

            internal HEMAobj(int period, int threshold) : this()   // do HEMA() first
            {
                Period = period;
                Threshold = (double)threshold * 1e-6;
                sqrtPeriod = Math.Sqrt(Period);
                ema1 = new EMAobj(period, threshold);
                ema2 = new EMAobj(period / 2, threshold);
                ema3 = new EMAobj(sqrtPeriod, threshold);
            }

            internal void Init(double thePrice)  // Initialize Indicator
            {
                ema1.Init(thePrice);
                ema2.Init(thePrice);
                ema3.Init(thePrice);
                HEMAval = ema3.EMAval;
                isRrising = false;
                isFalling = false;
                trendDir = 0;
                prevState = 0;
                prevTrendDir = 0;
                trendChanged = false;
                stateChanged = false;
            }

            internal double Calc(double thePrice)  // Calculate Indicator values
            {
                // HEMA(n) = EMA(2 * EMA(n / 2) – EMA(n)), sqrt(n))
                if (Period < 1)
                    throw new Exception("HEMAcalc: period < 1, invalid !!");
                if (Threshold < 1e-10)
                    throw new Exception("HEMAcalc: Threshold < 1e-10, invalid !!");
                if(trendDir != 0)
                    prevTrendDir = trendDir;
                prevState = trendDir;
                ema1.Calc(thePrice);
                ema2.Calc(thePrice);
                double term3 = 2 * ema2.EMAval - ema1.EMAval;
                HEMAval = ema3.Calc(term3);
                isRrising = ema3.isRrising;
                isFalling = ema3.isFalling;
                trendDir = isRrising ? 1 : (isFalling ? -1 : 0);
                trendChanged = (trendDir * prevTrendDir < 0);
                stateChanged = (trendDir != prevState);
                return HEMAval;
            }
        }

        internal class EMAobj
        {
            double Period;
            double Threshold;
            double prevEMA;
            internal double EMAval;
            internal bool isRrising;
            internal bool isFalling;
            internal int trendDir;
            internal int prevTrendDir;
            internal bool trendChanged;
            double K;

            internal EMAobj()
            {
                Period = 12;
                Threshold = 1 * 1e-6;
                isRrising = false;
                isFalling = false;
                trendDir = 0;
                prevTrendDir = 0;
                trendChanged = false;
                prevEMA = double.MinValue;
                EMAval = double.MinValue;
                K = 1.0;
            }

            internal EMAobj(double period, int threshold) : this()
            {
                Period = period;
                Threshold = (double)threshold * 1e-6;
                K = 2.0 / (Period + 1.0);
            }

            internal void Init(double thePrice)
            {
                isRrising = false;
                isFalling = false;
                trendDir = 0;
                prevTrendDir = 0;
                trendChanged = false;
                prevEMA = thePrice;
                EMAval = thePrice;
            }

            internal double Calc(double thePrice)
            {
                if (Period < 1)
                    throw new Exception("EMA: period < 1, invalid !!");
                if (Threshold < 1e-10)
                    throw new Exception("EMA: Threshold < 1e-10, invalid !!");
                if (prevEMA == double.MinValue)
                    prevEMA = thePrice;
                //EMA = (Price[today] x K) + (EMA[prev] x (1 – K)); K = 2 / (N + 1)
                EMAval = (thePrice * K) + prevEMA * (1 - K);
                var diff = EMAval - prevEMA;
                prevEMA = EMAval;
                isRrising = (diff > Threshold);
                isFalling = (diff < -Threshold);
                if (trendDir != 0)
                    prevTrendDir = trendDir;
                trendDir = isRrising ? 1 : (isFalling ? -1 : 0);
                trendChanged = (trendDir * prevTrendDir < 0);
                return EMAval;
            }
        }
    }
}
