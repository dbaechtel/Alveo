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
    ///  The DTEMA class formulates the DTEMA Indicator on an Alveo currency chart.  
    ///    
    ///  The period parameter set the strength of the filtering by the DTEMA.  
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
    [Description("Alveo DTEMA Indicator v2")]
    public class DTEMAv2 : IndicatorBase
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

        //Buffers for Alveo DTEMA Indicator
        Array<double> UpTrend;          // upwards trend
        Array<double> DownTrend;        // downwards trend
        Array<double> Consolidation;    // in between UpTrend and DownTrend

        Bar b;              // holds latest chart Bar data from Alveo
        Bar prevBar;        // holds latest chart Bar data from Alveo
        int counted_bars;   // amount of bars of bars on chart already processed by the indicator.
        int e;              // number of bars for the indicator to calculate
        double thePrice;    // holds the currency pair price for the NDI calculation. In units of the bas currency.
        bool firstrun;      // firstrun = true on first execution of the Start function. False otherwise.
        DTEMAobj DTema;

        const string dataFileDir = "C:\\temp\\DTEMAv2\\";

        /// <summary>  
        ///  C# constructor for DTEMAv2 Class
        ///  called to initialize the class when class is created by Alveo
        /// </summary>  
        public DTEMAv2()
        {
            try
            {
                // Basic indicator initialization. Don't use this constructor to calculate values

                indicator_buffers = 3;              // 3 lines on Alveo chart require 3 buffers
                indicator_chart_window = true;
                firstrun = true;                    // initially set true for first run
                prevBar = null;

                IndPeriod = 31;                     // Initial value for DTEMAv2 period
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
            catch (Exception ex)  // Print don't function at this point in Alveo
            {
                Print("DTEMA: Exception: " + ex.Message);
                Print("DTEMA: " + ex.StackTrace);
            }
        }

        //+------------------------------------------------------------------+");
        //| Custom indicator initialization function                         |");
        //| Called by Alveo to initialize the DTEMAv2 Indicator at startup.  |");
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
                SetIndexLabel(0, "DTEMA(" + IndPeriod + ").Bull");   // Sets description for showing in the DataWindow and in the tooltip on Chart.
                SetIndexStyle(1, DRAW_LINE, STYLE_SOLID);       // repeat for all 3 buffers
                SetIndexLabel(1, "DTEMA(" + IndPeriod + ").Bear");
                SetIndexStyle(2, DRAW_LINE, STYLE_SOLID);
                SetIndexLabel(2, "DTEMA(" + IndPeriod + ").Mixed");

                // Sets the "short" name of a custom indicator to be shown in the DataWindow and in the chart subwindow.
                IndicatorShortName("DTEMA v2.0 (" + IndPeriod + "," + SlopeThreshold + ")");

                DTema = new DTEMAobj(IndPeriod, SlopeThreshold);

                Print("DTEMA: Started. [" + Chart.Symbol + "] tf=" + Period());      // Print this message to Alveo Log file on startup
            }
            catch (Exception ex)
            {
                Print("DTEMA: Init: Exception: " + ex.Message);
                Print("DTEMA: " + ex.StackTrace);
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
                if (firstrun)   // on first run, calculate DTEMA on all ChartBars data
                {
                    b = ChartBars[e - 1];           // b = refernec to oldest ChartBars data
                    prevBar = b;
                    thePrice = (double)(b.Open + b.Close) / 2.0;     // initialize DTEMA to thePrice of the oldest bar
                    DTema.Init(thePrice);
                    firstrun = false;               // firstrun initialization completed 
                }
                else // not firstrun; only calculate DTEMA on ChartBars not already processed
                {
                    counted_bars = IndicatorCounted();
                    if (counted_bars < 0)
                        throw new Exception("DTEMA: Start: invalid IndicatorCounted value.");            // invalid value
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
                        Print("DTEMA: Gap detected. gap=" + gap / Point + " points. " + b.BarTime.ToString());
                        DTema.Init(thePrice);
                    }
                    DTema.Calc(thePrice);                                        // calculate new DTEMA value
                    UpdateBuffers(i);                                           // update Alvelo Indicator buffers
                }
            }
            catch (Exception ex)    // catch and Print any exceptions that may have happened
            {
                Print("DTEMA: Start: Exception: " + ex.Message);
                Print("DTEMA: " + ex.StackTrace);
            }
            return 0;
        }

        /// <summary>  
        ///  UpdateBuffers - update Alveo Indicator buffers with new data
        /// <param name="entry">index value into buffer.</param>
        /// </summary>  
        internal void UpdateBuffers(int entry)
        {
            var DTEMAvalue = DTema.DTEMAval;
            var indx = e - entry - 1;               // put data into buffers in the reverse order, for Alveo.
            UpTrend[indx] = EMPTY_VALUE;            // Initialize with EMPTY_VALUE
            DownTrend[indx] = EMPTY_VALUE;
            Consolidation[indx] = EMPTY_VALUE;
            if (DTema.isRrising)   // if UpTrend
            {
                UpTrend[indx] = DTEMAvalue;           // UpTrend buffer gets EMAvalue
            }
            else if (DTema.isFalling) // Downtrend
            {
                DownTrend[indx] = DTEMAvalue;         // DownTrend buffer gets EMAvalue
            }
            else  // otherwise
            {
                Consolidation[indx] = DTEMAvalue;     // Consolidation buffer gets EMAvalue
            }
            if (DTema.stateChanged)                  // if trendDir changed from previous call
            {
                switch (DTema.prevState)          // place connecting EMAvalue into proper buffer to connect the lines
                {
                    case 1: // uptrend
                        UpTrend[indx] = DTEMAvalue;
                        break;
                    case -1: // downtrend
                        DownTrend[indx] = DTEMAvalue;
                        break;
                    case 0:  // consolidation
                        Consolidation[indx] = DTEMAvalue;
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

        internal class DTEMAobj
        {
            double Period;
            double Threshold;
            internal bool isRrising;
            internal bool isFalling;
            internal int trendDir;
            internal int prevTrendDir;
            internal int prevState;
            internal bool trendChanged;
            internal bool stateChanged;
            double prevDTEMA;     // previous DTEMA value
            internal double DTEMAval;      // latest caculated MDi value
            EMAobj EMA1;
            EMAobj EMA2;
            EMAobj EMA3;
            bool dump = false;
            const string dataFileDir = "C:\\temp\\";
            System.IO.StreamWriter dfile = null;

            internal DTEMAobj()
            {
                Period = 12;
                Threshold = 1 * 1e-6;
                isRrising = false;
                isFalling = false;
                prevState = 0;
                trendDir = 0;
                prevTrendDir = 0;
                trendChanged = false;
                stateChanged = false;
                prevDTEMA = double.MinValue;    // previous DTEMA value
                DTEMAval = double.MinValue;     // latest caculated DTEMA value
            }

            internal DTEMAobj(int period, int threshold) : this()
            {
                if (period < 1)
                    throw new Exception("DTEMAobj: period < 1, invalid !!");
                if (threshold < 1)
                    throw new Exception("DTEMAobj: Threshold < 1, invalid !!");
                Period = (double)period;
                Threshold = (double)threshold * 1e-6;
                EMA1 = new EMAobj(Period, threshold);
                EMA2 = new EMAobj(Period, threshold);
                EMA3 = new EMAobj(Period, threshold);
                if (dump)
                {
                    string msg = "thePrice,EMA1,EMA2,EMA3,DTEMAval,prevDTEMA,trendDir,prevTrendDir,trendChanged";
                    dumpData(msg, false);
                }
            }

            internal void Init(double thePrice)
            {
                isRrising = false;
                isFalling = false;
                prevState = 0;
                trendDir = 0;
                prevTrendDir = 0;
                trendChanged = false;
                stateChanged = false;
                prevDTEMA = thePrice;
                DTEMAval = thePrice;
                EMA1.Init(thePrice);
                EMA2.Init(thePrice);
                EMA3.Init(thePrice);
            }

            internal double Calc(double thePrice)
            {
                EMA1.Calc(thePrice);
                EMA2.Calc(EMA1.EMAval);
                EMA3.Calc(EMA2.EMAval);
                var DEMAval = 2 * EMA1.EMAval - EMA2.EMAval;
                var TEMAval = 3 * EMA1.EMAval - 3 * EMA2.EMAval + EMA3.EMAval;
                DTEMAval = (DEMAval + TEMAval) / 2;
                var diff = DTEMAval - prevDTEMA;
                prevDTEMA = DTEMAval;
                isRrising = (diff > Threshold);
                isFalling = (diff < -Threshold);
                if (trendDir != 0)
                    prevTrendDir = trendDir;
                prevState = trendDir;
                trendDir = isRrising ? 1 : (isFalling ? -1 : 0);
                trendChanged = (trendDir * prevTrendDir < 0);
                stateChanged = (trendDir != prevState);
                string msg = thePrice
                    + "," + EMA1.EMAval
                    + "," + EMA2.EMAval
                    + "," + EMA3.EMAval
                    + "," + DTEMAval
                    + "," + prevDTEMA
                    + "," + trendDir
                    + "," + prevTrendDir
                    + "," + trendChanged
                    ;
                dumpData(msg);

                return DTEMAval;
            }

            internal void dumpData(string line, bool append = true)
            {
                if (!System.IO.Directory.Exists(dataFileDir))
                    System.IO.Directory.CreateDirectory(dataFileDir);
                var filename = "DTEMAdump.csv";
                dfile = new System.IO.StreamWriter(dataFileDir + filename, append);
                dfile.WriteLine(line);
                dfile.Close();
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
                    throw new Exception("EMAobj: period < 1, invalid !!");
                if (Threshold < 1e-10)
                    throw new Exception("EMAobj: Threshold < 1e-10, invalid !!");
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
