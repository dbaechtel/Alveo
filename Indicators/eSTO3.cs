using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using Alveo.Interfaces.UserCode;
using Alveo.Common.Classes;

namespace Alveo.UserCode
{
    [Serializable]
    [Description("Improved Stochastic Oscillator Indicator")]
    public class eSTO3 : IndicatorBase
    {
        internal class HSTO
        {
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
                internal bool firstrun;

                // HEMAobj constructor
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

                // HEMAobj constructor with input parameters
                internal HEMAobj(int period, int threshold = 0) : this()   // do HEMA() first
                {
                    Period = period;
                    Threshold = (double)threshold * 1e-6;
                    sqrtPeriod = Math.Sqrt(Period);
                    ema1 = new EMAobj(period, threshold);
                    ema2 = new EMAobj(period / 2, threshold);
                    ema3 = new EMAobj(sqrtPeriod, threshold);
                    firstrun = true;
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
                    firstrun = false;
                }

                internal double Calc(double thePrice)  // Calculate Indicator values
                {
                    // HEMA(n) = EMA(2 * EMA(n / 2) – EMA(n)), sqrt(n))
                    if (Period < 1)
                        throw new Exception("HEMAcalc: period < 1, invalid !!");
                    if (Threshold < 0)
                        throw new Exception("HEMAcalc: Threshold < 0, invalid !!");
                    if (firstrun)
                    {
                        Init(thePrice);
                    }
                    if (trendDir != 0)
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
                internal int prevState;
                internal bool trendChanged;
                internal bool stateChanged;
                internal double K;

                internal EMAobj()
                {
                    Period = 12;
                    Threshold = 1 * 1e-6;
                    isRrising = false;
                    isFalling = false;
                    trendDir = 0;
                    prevTrendDir = 0;
                    prevState = 0;
                    trendChanged = false;
                    stateChanged = false;
                    prevEMA = double.MinValue;
                    EMAval = double.MinValue;
                    K = 1.0;
                }

                internal EMAobj(double period, int threshold = 0) : this()
                {
                    Period = period;
                    Threshold = (double)threshold * 1e-6;
                    K = 2.0 / ((double)Period + 1.0);
                }

                internal void Init(double thePrice)
                {
                    isRrising = false;
                    isFalling = false;
                    trendDir = 0;
                    prevState = 0;
                    prevTrendDir = 0;
                    trendChanged = false;
                    stateChanged = false;
                    prevEMA = thePrice;
                    EMAval = thePrice;
                }

                internal double Calc(double thePrice)
                {
                    if (Period < 1)
                        throw new Exception("EMAobj: period < 1, invalid !!");
                    if (Threshold < 0)
                        throw new Exception("EMAobj: Threshold < 0, invalid !!");
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
                    prevState = trendDir;
                    trendDir = isRrising ? 1 : (isFalling ? -1 : 0);
                    trendChanged = (trendDir * prevTrendDir < 0);
                    stateChanged = (trendDir != prevState);
                    return EMAval;
                }
            }

            eSTO3 ea;
            HEMAobj emaLow;
            HEMAobj emaHigh;
            HEMAobj emaClose;
            Queue<double> LowestLowQ;
            Queue<double> HighestHighQ;

            internal int K_period;
            internal int D_period;
            internal double Threshold;

            internal double smoothedK;
            internal double smoothedD;
            double prevSmoothedK;
            double prevSmoothedD;

            internal bool STOdone;
            internal bool pctKfalling;
            internal bool pctKrising;
            internal bool pctDfalling;
            internal bool pctDrising;
            int counter;

            internal HSTO()
            {
                emaLow = null;
                emaHigh = null;
                emaClose = null;
                LowestLowQ = new Queue<double>();
                HighestHighQ = new Queue<double>();
                K_period = 0;
                D_period = 0;
                Threshold = 0;
                STOdone = false;
                pctKfalling = false;
                pctKrising = false;
                pctDfalling = false;
                pctDrising = false;
                smoothedK = -1;
                smoothedD = -1;
                prevSmoothedK = 0;
                prevSmoothedD = 0;
            }

            internal HSTO(eSTO3 eaIn, int k_period, int d_period, int threshold) : this()
            {
                ea = eaIn;
                K_period = k_period;
                D_period = d_period;
                Threshold = (double)threshold * 1e-7;
                emaLow = new HEMAobj(k_period);
                emaHigh = new HEMAobj(k_period);
                emaClose = new HEMAobj(k_period);
            }

            internal void Init(Bar b)
            {
                ea.Print("STO Init.");
                emaLow.Init((double)b.Low);
                emaHigh.Init((double)b.High);
                emaClose.Init((double)b.Close);
                LowestLowQ.Clear();
                HighestHighQ.Clear();
                counter = 0;
            }

            internal void Calc(Bar theBar)
            {
                //ea.Print(theBar.Close);
                // % K = (Current Close - Lowest Low)/ (Highest High - Lowest Low) *100
                // Full % K = Fast % K smoothed with X - period SMA
                // Full %D = X-period SMA of Full %K
                if (theBar == null)
                    throw new Exception("Stochastic.calc: theBar==null.");
                var d = (double)D_period;
                var alpha = 1 / (d + 1) + 1 / (2 * d);
                emaLow.Calc((double)theBar.Low);
                emaHigh.Calc((double)theBar.High);
                emaClose.Calc((double)theBar.Close);
                LowestLowQ.Enqueue((double)emaLow.HEMAval);
                while (LowestLowQ.Count > K_period)
                    LowestLowQ.Dequeue();
                var min = LowestLowQ.Min();
                HighestHighQ.Enqueue(emaHigh.HEMAval);
                while (HighestHighQ.Count > K_period)
                    HighestHighQ.Dequeue();
                var max = HighestHighQ.Max();
                var diff = max - min;
                var pctK = 50.0;
                if (diff > 0)
                    pctK = Math.Min(Math.Max(100 * (emaClose.HEMAval - min) / diff, 0), 100);
                if (smoothedK < 0)
                {
                    smoothedK = pctK;
                    smoothedD = pctK;
                }
                prevSmoothedK = smoothedK;
                prevSmoothedD = smoothedD;
                smoothedK += alpha * (pctK - smoothedK);
                smoothedD += alpha * (smoothedK - smoothedD);
                var diff1 = smoothedK - prevSmoothedK;
                var diff2 = smoothedD - prevSmoothedD;
                pctKrising = (diff1 > Threshold);
                pctKfalling = (diff1 < -Threshold);
                pctDrising = (diff2 > Threshold);
                pctDfalling = (diff2 < -Threshold);
                STOdone = (LowestLowQ.Count >= K_period);
                counter++;
            }
        }

        private readonly Array<double> _highesBuffer = new Array<double>();
        private readonly Array<double> _lowesBuffer = new Array<double>();
        private readonly Array<double> _mainBuffer = new Array<double>();
        private readonly Array<double> _signalBuffer = new Array<double>();
        private List<Array<double>> _levels;

        private int draw_begin1;
        private int draw_begin2;
        int nBuffered;

        HSTO sto;

        public eSTO3()
        {
            nBuffered = 0;
            indicator_buffers = 2;
            indicator_chart_window = false;
            KPeriod = 5;
            DPeriod = 3;
            Threshold = 100;  // * 1e-7
            PriceType = PriceConstants.PRICE_CLOSE;

            indicator_color1 = LightSeaGreen;
            indicator_color2 = Red;

            Levels.Values.Add(new Alveo.Interfaces.UserCode.Double(70));
            Levels.Values.Add(new Alveo.Interfaces.UserCode.Double(30));

            var short_name = "eSTO3(" + KPeriod + "," + DPeriod + ")";
            IndicatorShortName(short_name);
            SetIndexLabel(0, short_name);
            SetIndexLabel(1, "Signal");
        }

        [Description("%K Period of the Stochastic Indicator")]
        [Category("Settings")]
        [DisplayName("K-Period")]
        public int KPeriod { get; set; }

        [Description("%D Period of the Stochastic Indicator")]
        [Category("Settings")]
        [DisplayName("D-Period")]
        public int DPeriod { get; set; }

        [Description("Threshold for dircetion change")]
        [Category("Settings")]
        public int Threshold { get; set; }

        [Description("Moving Average type on witch Stochastic will be calculated")]
        [Category("Settings")]
        [DisplayName("MA Type")]
        public MovingAverageType MAType { get; set; }

        [Description("Price type on witch Stochastic will be calculated")]
        [Category("Settings")]
        [DisplayName("Price Type")]
        public PriceConstants PriceType { get; set; }

        protected override int Init()
        {
            nBuffered = 0;
            sto = new HSTO(this, KPeriod, DPeriod, Threshold);
            for (int x = 2; x < indicator_buffers; x++)
            {
                SetIndexBuffer(x, null);
            }
            indicator_buffers = Levels.Values.Count + 2;
            string short_name;

            SetIndexStyle(0, DRAW_LINE);
            SetIndexBuffer(0, _mainBuffer);
            SetIndexStyle(1, DRAW_LINE);
            SetIndexBuffer(1, _signalBuffer);

            short_name = "eSTO3(" + KPeriod + "," + DPeriod + ")";
            IndicatorShortName(short_name);
            SetIndexLabel(0, short_name);
            SetIndexLabel(1, "Signal");

            draw_begin1 = KPeriod;
            draw_begin2 = draw_begin1 + DPeriod;
            //SetIndexDrawBegin(0, draw_begin1);
            // SetIndexDrawBegin(1, draw_begin2);

            _levels = new List<Array<double>>();
            int i = 0;
            while (i < Levels.Values.Count)
            {
                Array<double> _newLevel = new Array<double>();
                SetIndexLabel(i + 2, string.Format("Level {0}", i + 1));
                SetIndexStyle(i + 2, DRAW_LINE, (int)Levels.Style, Levels.Width, Levels.Color);
                SetIndexBuffer(i + 2, _newLevel);
                _levels.Add(_newLevel);
                i++;
            }
            SetIndexBuffer(i + 2, _highesBuffer);
            SetIndexBuffer(i + 3, _lowesBuffer);
            return (0);
        }

        protected override int Start()
        {
            try
            {
                int l = 0;                                      // draw levels
                foreach (Array<double> _lvl in _levels)
                {
                    for (int j = 0; j < _lvl.Count; j++)
                    {
                        _lvl[j] = Levels.Values[l].Value;
                    }
                    l++;
                }

                int i;
                var counted_bars = IndicatorCounted();
                Bar theBar;

                var e = Bars - counted_bars - 1;
                if (e < 1)                      // no data to process
                    return 0;                   // we're out of here, for now    

                var data = GetHistory(Symbol, TimeFrame);
                if (data.Count == 0)
                    return 0;
                if (nBuffered < 1)
                {
                    var cntB = Bars;
                    theBar = data[cntB - 1];
                    sto.Init(theBar);
                    for (i = 0; i < cntB; i++)
                    {
                        theBar = data[cntB - i - 1];
                        sto.Calc(theBar);
                        _mainBuffer[cntB - i - 1] = sto.smoothedK;
                        _signalBuffer[cntB - i - 1] = sto.smoothedD;
                        nBuffered++;
                    }
                }
                else
                {
                    var cnt = Bars - counted_bars;
                    for (i = 0; i < cnt; i++)
                    {
                        theBar = data[cnt - i - 1];
                        sto.Calc(theBar);
                        _mainBuffer[cnt - i - 1] = sto.smoothedK;
                        _signalBuffer[cnt - i - 1] = sto.smoothedD;
                        nBuffered++;
                    }
                }
            }
            catch (Exception e)
            {
                Print("Exception: " + e.Message);
                Print("Exception: " + e.StackTrace);
            }
            return (0);
        }

        public override bool IsSameParameters(params object[] values)
        {
            if (values.Length != 7)
                return false;
            if ((values[0] != null && Symbol == null) || (values[0] == null && Symbol != null))
                return false;
            if (values[0] != null && (!(values[0] is string) || (string)values[0] != Symbol))
                return false;
            if (!(values[1] is int) || (int)values[1] != TimeFrame)
                return false;
            if (!(values[2] is int) || (int)values[2] != KPeriod)
                return false;
            if (!(values[3] is int) || (int)values[3] != DPeriod)
                return false;
            if (!(values[4] is int) || (int)values[4] != Threshold)
                return false;
            if (!(values[5] is MovingAverageType) || (MovingAverageType)values[5] != MAType)
                return false;
            if (!(values[6] is PriceConstants) || (PriceConstants)values[6] != PriceType)
                return false;

            return true;
        }
    }
}