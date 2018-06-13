using System;
using System.Linq;
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
    [Description("Alveo ATR2 Indicator v1.0")]
    public class ATR2 : IndicatorBase
    {
        #region Properties

        // User settable Properties for this Alveo Indicator
        /// <param name="period">Sets the strength of the filtering.</param>
        [Category("Settings")]
        [DisplayName("Period. ")]
        [Description("Sets the strength of the filtering. [ex: 20]")]
        public int IndPeriod { get; set; }
        #endregion

        //Buffers for Alveo CCI2 Indicator
        Array<double> _ATR = null;

        Bar b;              // holds latest chart Bar data from Alveo
        int prevBars;
        int counted_bars;   // amount of bars of bars on chart already processed by the indicator.
        int e;              // number of bars for the indicator to calculate
        ATRObj atr;

        /// <summary>  
        ///  C# constructor for CCI2 Class
        ///  called to initialize the class when class is created by Alveo
        /// </summary>  
        public ATR2()
        {
            try
            {
                // Basic indicator initialization. Don't use this constructor to calculate values

                indicator_buffers = 1;              // lines on Alveo chart require buffers
                indicator_chart_window = false;
                b = null;

                IndPeriod = 10;                     // Initial value for CCI2 period

                indicator_width1 = 1;                       // width of line on the chart
                indicator_color1 = Colors.Red;              // line color

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
                prevBars = 0;
                atr = new ATRObj(IndPeriod);
                _ATR = new Array<double>();

                // ENTER YOUR CODE HERE
                IndicatorBuffers(indicator_buffers);        // Allocates memory for buffers used for custom indicator calculations.
                SetIndexBuffer(0, _ATR);                    // binds a specified indicator buffer with one-dimensional dynamic array of the type double.
                SetIndexArrow(0, 159);                      // Sets an arrow symbol for indicators line of the DRAW_ARROW type. 159=dot.

                SetIndexStyle(0, DRAW_LINE, STYLE_SOLID);       // Sets the shape, style, width and color for the indicator line.
                SetIndexLabel(0, "ATR2(" + IndPeriod + ")");    // Sets description for showing in the DataWindow and in the tooltip on Chart.

                // Sets the "short" name of a custom indicator to be shown in the DataWindow and in the chart subwindow.
                IndicatorShortName("Average True Range (" + IndPeriod + ")");

                Print("ATR2: Started. [" + Chart.Symbol + "] period=" + IndPeriod);      // Print this message to Alveo Log file on startup
            }
            catch (Exception ex)
            {
                Print("ATR2: Init: Exception: " + ex.Message);
                Print("ATR2: " + ex.StackTrace);
            }
            return 0;
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
                var nBars = Bars;
                if (nBars == prevBars)
                    return 0;
                prevBars = nBars;
                e = nBars - 1;   // e = largest index in ChartBars array
                if (e < 1)      // not enough data
                    return -1;

                counted_bars = IndicatorCounted();
                if (counted_bars < 0)
                    throw new Exception("ATR2: Start: invalid IndicatorCounted value.");            // invalid value
                e -= counted_bars;          // reduce e count by bars previously processed

                if (e < 1)                      // no data to process
                    return 0;                   // we're out of here, for now    

                for (int i = 0; i < e; i++)     // iterate each bar to process  
                {
                    var pos = e - i - 1;
                    b = ChartBars[pos];   // get oldest chart bar in array
                    atr.Calc(b);
                    UpdateBuffers(pos);           // update Alvelo Indicator buffers
                }
            }
            catch (Exception ex)    // catch and Print any exceptions that may have happened
            {
                Print("ATR2: Start: Exception: " + ex.Message);
                Print("ATR2: " + ex.StackTrace);
            }
            return 0;
        }

        /// <summary>  
        ///  UpdateBuffers - update Alveo Indicator buffers with new data
        /// <param name="pos"> pos value into buffer.</param>
        /// </summary>  
        internal void UpdateBuffers(int pos)
        {
            _ATR[pos] = atr.value;
        }

        //+------------------------------------------------------------------+
        //| AUTO GENERATED CODE. THIS METHODS USED FOR INDICATOR CACHING     |
        //+------------------------------------------------------------------+
        #region Auto Generated Code

        [Description("Parameters order Symbol, TimeFrame")]
        public override bool IsSameParameters(params object[] values)  // determine if Indicator parameter values have not changed.
        {
            if (values.Length != 3)
                return false;

            if (!CompareString(Symbol, (string)values[0]))
                return false;

            if (TimeFrame != (int)values[1])
                return false;

            if (IndPeriod != (int)values[2])
                return false;

            return true;
        }

        [Description("Parameters order Symbol, TimeFrame")]
        public override void SetIndicatorParameters(params object[] values)     // Set Indicator values from cache
        {
            if (values.Length != 3)
                throw new ArgumentException("Invalid parameters number");

            Symbol = (string)values[0];
            TimeFrame = (int)values[1];
            IndPeriod = (int)values[2];
        }

        #endregion  // Auto Generated Code

        internal class ATRObj
        {
            SMAobj sma;
            int IndPeriod;
            bool firstRun;
            internal double TR;
            internal double value;
            internal double prev_value;
            internal double prev_value2;
            Bar prevBar;

            ATRObj()
            {
                sma = null;
                firstRun = true;
                TR = 0;
                value = 0;
                prev_value = value;
                prev_value2 = prev_value;
                prevBar = null;
            }

            internal ATRObj(int period) : this()
            {
                IndPeriod = period;
                sma = new SMAobj(period);
            }

            internal void Init(Bar b = null)
            {
                sma.Init(0);
                TR = 0;
                value = 0;
                prev_value = value;
                prev_value2 = prev_value;
                prevBar = b;
            }

            internal double Calc(Bar b)
            {
                if (firstRun)
                {
                    Init(b);
                    firstRun = false;
                }
                else
                {
                    prev_value2 = prev_value;
                    prev_value = value;
                    value = 0;
                    TR = (double)Math.Max(Math.Abs(b.High - b.Low),
                        Math.Max(Math.Abs(b.High - prevBar.Close), Math.Abs(prevBar.Close - b.Low)));
                    value = sma.Calc(TR);
                    prevBar = b;
                }
                return value;
            }
        }

        internal class SMAobj
        {
            double Period;
            internal double value;
            internal double prev_value;
            internal double prev_value2;
            Queue<double> Qprices;
            bool firstRun;

            // EMAobj constructor
            internal SMAobj()
            {
                Period = 10;
                value = double.MinValue;
                prev_value = value;
                prev_value2 = prev_value;
                Qprices = new Queue<double>();
                firstRun = true;
            }

            // EMAobj constructor with input parameters
            internal SMAobj(double period) : this()  // call SMAobj() first
            {
                Period = period;
            }

            // Initialize EMAobj
            internal void Init(double input)
            {
                value = input;
                prev_value = value;
                prev_value2 = prev_value;
                Qprices.Clear();
                Qprices.Enqueue(input);
            }

            // Calculate SMAobj;
            internal double Calc(double input)
            {
                if (Period < 1)
                    throw new Exception("SMA: period < 1, invalid !!");
                if (firstRun)
                {
                    Init(input);
                    Qprices.Enqueue(input);
                    firstRun = false;
                }
                else  // !firstrun
                {
                    prev_value2 = prev_value;
                    prev_value = value;
                    value = 0;
                    Qprices.Enqueue(input);
                    while (Qprices.Count > Period)
                    {
                        Qprices.Dequeue();
                    }
                    var arr = Qprices.ToArray();
                    var count = arr.Count();
                    if (count > 0)
                    {
                        double sum = arr.Sum();
                        value = sum / count;
                    }
                }
                return value;
            }
        }
    }
}
