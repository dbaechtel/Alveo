
I have posted the source code to 3 new Alveo Custom Indicators at https://github.com/dbaechtel/Alveo/tree/master/Indicators.

The source code for the indicators are a good study of how to write good Indicator code for Alveo.

* ADXi - an improved version of the Alveo Average Directional Index (ADX) indicator.
Alveo has an error where they divide the +DM and -DM by TR instead of the ATR as the ADX is defined.
This ADXi indicator is corrected and seems to react quicker and with greater range than the Alveo version.
This indicator source has:
  * code comments to explain what is happening
  * exception handling
  * division by zero avoidance
  * range bounding
  * correction of Alveo quick fix to ADX code
  * ADX Class object encapsulation

* EMA3 - a 3 color version of the Alveo EMA indicator.

* DTEMAv2 - version 2 of the Dual-Tripple EMA Indicator. 
This indicator is the average of a Dual EMA and a Triple EMA for the best of both.

* HEMA - the Honest EMA indicator that uses the Hull Moving Average equation, substituting EMAs for the WMA functions.

Look carefully at techniques like the encapsulation used in the EMAobj class in EMA3.
This encapsulation allows you to include all of the variables and methods used by the EMA into a single object class.
This encapsulation allows you to segement the code and provide a "separation of concerns", which is helpful.

The object class allows you to easily copy the object class and resuse it in multiple projects (such as Expert Advisers).
If you use the same object class in both Indicators and Expert Advisors, then you can be sure that the functionality is identical.

If you use the same object in an Indicator and Expert Adviser, then you can see what the Expert Adviser has calculated that will assist you in adjusting the EA Settings.

Let me know what you think of them.

dbaechtel@gmail.com


