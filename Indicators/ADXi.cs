using System;
using System.ComponentModel;
using System.Windows.Media;
using Alveo.Interfaces.UserCode;
using Alveo.Common.Classes;

namespace Alveo.UserCode
{
    [Serializable]
    [Description("Average Directional Movement Indicator")]
    public class ADXi : IndicatorBase
    {
        const string version = "ADXi version 1.2";

        // arrays for indicator line buffers
        private readonly Array<double> _adxARR;
        private readonly Array<double> _minusDi;
        private readonly Array<double> _plusDi;

        ADXobj adx;     // internal ADX indicator class object

        public ADXi()
        {
            indicator_buffers = 3;                      // 3 incator buffers for 3 lines on chart
            IndicatorPeriod = 10;                       // default indicator Period
            indicator_chart_window = false;             // separate indicator window

            // define 3 indicator labels
            indicator_color1 = Colors.Blue;
            SetIndexLabel(0, string.Format("ADX({0})", IndicatorPeriod));
            indicator_color2 = Colors.Green;
            SetIndexLabel(1, "Plus_Di");
            indicator_color3 = Colors.Red;
            SetIndexLabel(2, "Minus_Di");

            PriceType = PriceConstants.PRICE_CLOSE;     // defailt PriceType for indicator

            _adxARR = new Array<double>();              // allocate 3 array buffers
            _plusDi = new Array<double>();
            _minusDi = new Array<double>();
        }

        #region UserSettings
        [Description("Period of the ADX Indicator in Bars. [ex: 10]")]
        [Category("Settings")]
        [DisplayName("Period")]
        public int IndicatorPeriod { get; set; }

        [Description("Price type with witch ADX will be calculated.")]
        [Category("Settings")]
        [DisplayName("Price type")]
        public PriceConstants PriceType { get; set; }
        #endregion

        // called by Alveo to Initialize the Indicator
        protected override int Init()
        {
            try
            {
                SetIndexLabel(0, string.Format("ADXi({0})", IndicatorPeriod));
                SetIndexLabel(1, "Plus_Di");
                SetIndexLabel(2, "Minus_Di");

                IndicatorShortName(string.Format("ADXi({0})", IndicatorPeriod));

                SetIndexBuffer(0, _adxARR);
                SetIndexBuffer(1, _plusDi);
                SetIndexBuffer(2, _minusDi);

                adx = new ADXobj(IndicatorPeriod);      // create ADX indicator object
            }
            catch (Exception e)     // if something goes wrong
            {
                Print("ADXi Exception: " + e.Message);
                Print("ADXi Exception: " + e.StackTrace);
            }

            return 0;
        }

        // Called by Alveo every tick and every new bar
        protected override int Start()
        {
            try
            {
                var pos = Bars - IndicatorCounted();                // how many bars to be processed
                var data = GetHistory(Symbol, TimeFrame);           // get chart bars
                if (data.Count == 0 || Bars == 0 || pos < 1)        // nothing to do
                    return 0;

                var baseArray = GetPrice(GetHistory(Symbol, TimeFrame), PriceType);     // get selected PriceType data into array

                var exp = 2 / (double)(IndicatorPeriod + 1);        // calculate filter factor
                if (pos >= data.Count)                              // if not enough data
                    pos = data.Count - 1;                           // then process less bars
                while (pos >= 0)                                    // process all of the requested bars
                {
                    if (pos >= Bars - 1)                            // if not enough chart bars
                    {
                        pos = Bars - 1;                             // process less bars
                        _plusDi[pos] = 0;                           // initialize buffer values
                        _minusDi[pos] = 0;
                        _adxARR[pos] = 0;
                    }
                    else  // Calculate ADX and update display buffers with results
                    {
                        adx.Calc(data[pos], _adxARR[pos + 1], _plusDi[pos + 1], _minusDi[pos + 1]);
                        _plusDi[pos] = adx._plusDi;
                        _minusDi[pos] = adx._minusDi;
                        _adxARR[pos] = adx._adx;
                    }
                    pos--;      // next buffer position
                }
            }
            catch (Exception e) // in case something goes wrong
            {
                Print("ADXi Exception: " + e.Message);      // tells you what
                Print("ADXi Exception: " + e.StackTrace);   // tells you where
            }
            return 0;
        }

        public override bool IsSameParameters(params object[] values)
        {
            // have any of the User Settings changed
            if (values.Length != 4)
                return false;
            if ((values[0] != null && Symbol == null) || (values[0] == null && Symbol != null))
                return false;
            if (values[0] != null && (!(values[0] is string) || (string)values[0] != Symbol))
                return false;
            if (!(values[1] is int) || (int)values[1] != TimeFrame)
                return false;
            if (!(values[2] is int) || (int)values[2] != IndicatorPeriod)
                return false;
            if (!(values[3] is PriceConstants) || (PriceConstants)values[3] != PriceType)
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
            IndicatorPeriod = (int)values[2];
            PriceType = (PriceConstants)values[3];
        }

        // ADX indicator clas object
        internal class ADXobj
        {
            // variables
            double pdm;
            double mdm;
            double tr;
            double price_high;
            double price_low;
            internal double plusSdi;
            internal double minusSdi;
            double temp;
            int IndicatorPeriod;
            double exp;
            internal double _plusDi;
            internal double _minusDi;
            internal double _adx;
            double atr;
            int counter;
            Bar prevBar;

            internal ADXobj()   // class constuctor
            {
                // nothing to do
            }

            internal ADXobj(int period) : this()    // parameterized construdtor
            {
                IndicatorPeriod = period;
                Init();
            }

            internal void Init(Bar theBar = null)
            {
                // initialize variables
                exp = Math.Max(Math.Min(2 / (double)(IndicatorPeriod + 1), 1), 0);  // filter factor
                atr = 0;
                tr = 0;
                counter = 0;
                prevBar = theBar;
                _plusDi = 0;
                _minusDi = 0;
                _adx = 0;
            }

            internal double Calc(Bar theBar, double prevAdx, double prevPlusDi, double prevMinusDi)
            {
                // calculate ADX value 
                if (prevBar == null)    // no prevBar, initialize
                {
                    Init(theBar);
                    return 0;
                }
                counter = Math.Max(++counter, IndicatorPeriod); // for atr calculation
                price_low = (double)theBar.Low;
                price_high = (double)theBar.High;
                pdm = price_high - (double)prevBar.High;
                mdm = (double)prevBar.Low - price_low;
                if (pdm < 0)
                    pdm = 0; // +DM
                if (mdm < 0)
                    mdm = 0; // -DM
                if (pdm.Equals(mdm))
                {
                    pdm = 0;
                    mdm = 0;
                }
                else if (pdm < mdm)
                    pdm = 0;
                else if (mdm < pdm)
                    mdm = 0;
                var num1 = Math.Abs(price_high - price_low);
                var num2 = Math.Abs(price_high - (double)prevBar.Close);
                var num3 = Math.Abs(price_low - (double)prevBar.Close);
                tr = Math.Max(num1, num2);                          // TR
                tr = Math.Max(tr, num3);
                atr = (atr * (counter - 1) + tr) / counter;         // ATR
                if (atr <= 0)       // avoid division by zero
                {
                    plusSdi = 0;
                    minusSdi = 0;
                }
                else
                {
                    plusSdi = 100.0 * Math.Min(Math.Max(pdm / atr, 0), 1.0);
                    minusSdi = 100.0 * Math.Min(Math.Max(mdm / atr, 0), 1.0);
                }

                _plusDi = Math.Max(Math.Min(plusSdi * exp + prevPlusDi * (1 - exp), 100), 0);
                _minusDi = Math.Max(Math.Min(minusSdi * exp + prevMinusDi * (1 - exp), 100), 0);
                var div = Math.Abs(_plusDi + _minusDi);
                if (div <= 0)       // avoid division by zero
                    temp = 0;
                else
                    temp = 100 * (Math.Abs(_plusDi - _minusDi) / div);
                _adx = Math.Max(Math.Min(temp * exp + prevAdx * (1 - exp), 100), 0);    // ADX value
                prevBar = theBar;
                return _adx;
            }
        }
    }
}