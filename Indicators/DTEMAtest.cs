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
    ///  The DTEMA class formulates the DTEMA Indicator on an Alveo chart.  
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
    [Description("Alveo DTEMA Indicator v1.1")]
    public class DTEMAtest 
    {
        #region Properties

        // User settable Properties for this Alveo Indicator
        /// <param name="period">Sets the strength of the filtering.</param>
        [Category("Settings")]
        [DisplayName("MA Period. ")]
        [Description("Sets the strength of the filtering. [ex: 20]")]
        public int period { get; set; }

        /// <param name="slopeThreshold">Specifies at what slope Uptrend and Downtrend are determined.</param>
        [Category("Settings")]
        [DisplayName("Slope Trheshold * 1e-6. ")]
        [Description("Specifies at what slope Uptrend and Downtrend are determined. [ex: 10]")]
        public double slopeThreshold { get; set; }
        #endregion

        //Buffers for Alveo DTEMA Indicator
        Array<double> UpTrend;          // upwards trend
        Array<double> DownTrend;        // downwards trend
        Array<double> Consolidation;    // in between UpTrend and DownTrend

        int Bars;

        Bar b;              // holds latest chart Bar data from Alveo
        internal List<Bar> ChartBars;
        int counted_bars;   // amount of bars of bars on chart already processed by the indicator.
        int e;              // number of bars for the indicator to calculate
        double prevDTEMA;     // previous DTEMA value
        double newDTEMA;      // latest caculated MDi value
        double DTEMAslope;    // filtered slope of indicator. Used to dtermine Uptrend or Downtrend.
        double thePrice;    // holds the currency pair price for the NDI calculation. In units of the bas currency.
        bool firstrun;      // firstrun = true on first execution of the Start function. False otherwise.
        int trendDir;       // Trend direction: +1 = uptrend, -1 = downtrend, 0 = between uptrend and downtrend.
        int prevTrendDir;   // previous trendDir
        double prevEMA = double.MinValue;
        double prevEMA2 = double.MinValue;
        double prevEMA3 = double.MinValue;
        double EMAn;
        double EMA2;
        double EMA3;

        /// <summary>  
        ///  C# constructor for DTEMA Class
        ///  called to initialize the class when class is created by Alveo
        /// </summary>  
        public DTEMAtest()
        {
            try
            {
                // Basic indicator initialization. Don't use this constructor to calculate values

                firstrun = true;                // initially set true
                trendDir = 0;                   // trend direction is initially undetermined
                prevTrendDir = 0;
                prevEMA = double.MinValue;
                prevEMA2 = double.MinValue;
                prevEMA3 = double.MinValue;

                period = 25;                    // Initial value for DTEMA period
                slopeThreshold = 100;            // Initial value for slopeThreshold

                UpTrend = new Array<double>();      // 3 data buffers for Alveo Indicator
                DownTrend = new Array<double>();
                Consolidation = new Array<double>();
            }
            catch (Exception e)
            {
                string msg = e.Message;
            }
        }

        //+------------------------------------------------------------------+");
        //| Custom indicator initialization function                         |");
        //| Called by Alveo to initialize the DTEMA Indicator at startup.      |");
        //+------------------------------------------------------------------+");
        internal int Init()
        {
            // Sets the "short" name of a custom indicator to be shown in the DataWindow and in the chart subwindow.

            ChartBars = new List<Bar>();
            prevDTEMA = double.MinValue;          // dummy value to indicate that prevDTEMA is not yet assigned a value.

            return 0;   // done
        }

        //+------------------------------------------------------------------+");
        //| Custom indicator deinitialization function                       |");
        //| Called by Alveo when the Indicator is closed                     |");
        //+------------------------------------------------------------------+");
        internal int Deinit()
        {
            // ENTER YOUR CODE HERE
            return 0;
        }

        /// <summary>  
        ///  DTEMAcalc - calculates the new DTEMA value
        /// <param name="prevDTEMA">Previous DTEMA value.</param>
        /// <param name="thePrice">Currency Price at the current chart Bar.</param>
        /// <param name="period">Strength of data filtering.</param>
        /// </summary>  

        protected double DTEMAcalc(double thePrice, int period)
        {
            if (prevEMA == double.MinValue)
                prevEMA = thePrice;
            if (prevEMA2 == double.MinValue)
                prevEMA2 = thePrice;
            if (prevEMA3 == double.MinValue)
                prevEMA3 = thePrice;
            EMAn = EMA(prevEMA, thePrice, period);
            prevEMA = EMAn;
            EMA2 = EMA(prevEMA2, EMAn, period);
            prevEMA2 = EMA2;
            EMA3 = EMA(prevEMA3, EMA2, period);
            prevEMA3 = EMA3;
            var DEMAval = 2 * EMAn - EMA2;
            var TEMAval = 3 * EMAn - 3 * EMA2 + EMA3;
            var DTEMAval = (DEMAval + TEMAval) / 2;
            return DTEMAval;
        }

        protected double EMA(double prevEMA, double thePrice, int period)
        {
            //EMA = (Price[today] x K) + (EMA[prev] x (1 – K)); K = 2 / (N + 1)
            double K = 2.0 / ((double)period + 1.0);
            var EMAval = (thePrice * K) + prevEMA * (1 - K);
            return EMAval;
        }

        internal int doStart()
        {
            Bars = ChartBars.Count;
            return this.Start();
        }

        //+--------------------------------------------------------------------------+");
        //| Custom indicator iteration function                                      |");
        //| Called by Alveo everytime a new chart bar appears, and maybe more often  |");
        //+--------------------------------------------------------------------------+");
        internal int Start()
        {
            try  // to catch and handle Exceptions that might occur in this code block
            {
                e = Bars - 1;   // e = largest index in ChartBars array
                if (e < 2)      // not enough data
                    return -1;

                if (firstrun)   // on first run, calculate DTEMA on all ChartBars data
                {
                    b = ChartBars[e - 1];           // b = refernec to oldest ChartBars data
                    thePrice = (double)b.Close;     // initialize prevDTEMA to thePrice of the oldest bar
                    prevDTEMA = thePrice;
                    firstrun = false;               // firstrun initialization completed 
                }
                else // not firstrun; only calculate DTEMA on ChartBars not already processed
                {
                    counted_bars = IndicatorCounted();
                    if (counted_bars < 0)
                        return (-1);            // invalid value
                    e -= counted_bars;          // reduce e count by bars previously processed
                }

                if (e < 1)                      // no data to process
                    return 0;                   // we're out of here, for now    

                for (int i = 0; i < e; i++)     // iterate each bar to process  
                {
                    b = ChartBars[e - i - 1];                                   // get oldest chart bar in array
                    thePrice = (double)b.Close;                                 // get bar Price for DTEMA calc
                    newDTEMA = DTEMAcalc(thePrice, period);                     // calculate new DTEMA value
                    DTEMAslope = 0 * DTEMAslope + 1.0 * (newDTEMA - prevDTEMA);  // calculate filtered DTEMA slope value
                    prevDTEMA = newDTEMA;                                       // save prevDTEMA for next iteratiom
                    prevTrendDir = trendDir;                                    // save previous trendDir
                }
            }
            catch (Exception ex)    // catch and Print any exceptions that may have happened
            {
                Print("DTEMA: " + ex.Message);
                Print("DTEMA: " + ex.StackTrace);
            }
            return 0;
        }

        internal void Print(string msg)
        {
            Console.WriteLine(msg);
        }

        internal int IndicatorCounted()
        {
            return Bars;
        }
    }
}
