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
        private readonly Array<double> _adxARR;
        private readonly Array<double> _minusDi;
        private readonly Array<double> _plusDi;

        ADXobj adx;

        public ADXi()
        {
            indicator_buffers = 3;
            IndicatorPeriod = 10;
            indicator_chart_window = false;

            indicator_color1 = Colors.Blue;
            SetIndexLabel(0, string.Format("ADX({0})", IndicatorPeriod));
            indicator_color2 = Colors.Green;
            SetIndexLabel(1, "Plus_Di");
            indicator_color3 = Colors.Red;
            SetIndexLabel(2, "Minus_Di");

            PriceType = PriceConstants.PRICE_CLOSE;
            _adxARR = new Array<double>();
            _plusDi = new Array<double>();
            _minusDi = new Array<double>();
        }

        [Description("Period of the ADX Indicator")]
        [Category("Settings")]
        [DisplayName("Period")]
        public int IndicatorPeriod { get; set; }

        [Description("Price type on witch ADX will be calculated")]
        [Category("Settings")]
        [DisplayName("Price type")]
        public PriceConstants PriceType { get; set; }

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

                adx = new ADXobj(IndicatorPeriod);
            }
            catch (Exception e)
            {
                Print("ADXi Exception: " + e.Message);
                Print("ADXi Exception: " + e.StackTrace);
            }

            return 0;
        }

        protected override int Start()
        {
            try
            {
                var pos = Bars - IndicatorCounted();
                var data = GetHistory(Symbol, TimeFrame);
                if (data.Count == 0 || Bars == 0 || pos < 1)
                    return 0;
                var baseArray = GetPrice(GetHistory(Symbol, TimeFrame), PriceType);

                var exp = 2 / (double)(IndicatorPeriod + 1);
                if (pos >= data.Count)
                    pos = data.Count - 1;
                while (pos >= 0)
                {
                    if (pos >= Bars - 1)
                    {
                        pos = Bars - 1;
                        _plusDi[pos] = 0;
                        _minusDi[pos] = 0;
                        _adxARR[pos] = 0;
                    }
                    else
                    {
                        adx.Calc(data[pos], _adxARR[pos + 1], _plusDi[pos + 1], _minusDi[pos + 1]);
                        _plusDi[pos] = adx._plusDi;
                        _minusDi[pos] = adx._minusDi;
                        _adxARR[pos] = adx._adx;
                    }
                    pos--;
                }
            }
            catch (Exception e)
            {
                Print("ADXi Exception: " + e.Message);
                Print("ADXi Exception: " + e.StackTrace);
            }
            return 0;
        }

        public override bool IsSameParameters(params object[] values)
        {
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

        internal class ADXobj
        {
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

            internal ADXobj()
            {

            }

            internal ADXobj(int period) : this()
            {
                IndicatorPeriod = period;
                Init();
            }

            internal void Init(Bar theBar = null)
            {
                exp = Math.Max(Math.Min(2 / (double)(IndicatorPeriod + 1), 1), 0);
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
                if (prevBar == null)
                {
                    Init(theBar);
                    return 0;
                }
                counter = Math.Max(++counter, IndicatorPeriod);
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
                tr = Math.Max(num1, num2);
                tr = Math.Max(tr, num3);
                atr = (atr * (counter - 1) + tr) / counter;
                if (tr.Equals(0))
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
                if (div <= 0)
                    temp = 0;
                else
                    temp = 100 * (Math.Abs(_plusDi - _minusDi) / div);
                _adx = Math.Max(Math.Min(temp * exp + prevAdx * (1 - exp), 100), 0);
                prevBar = theBar;
                return 0;
            }
        }
    }
}