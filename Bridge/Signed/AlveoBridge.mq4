
#property copyright "(c) 2019, Entity3 LLC"
#property link ""

#import "nquotes/nquoteslib.ex4"
	int nquotes_setup(string className, string assemblyName);
	int nquotes_init();
	int nquotes_start();
	int nquotes_deinit();
	double nquotes_on_tester();
	int nquotes_on_timer();
	int nquotes_on_chart_event(int id, long lparam, double dparam, string sparam);

	int nquotes_set_property_bool(string name, bool value);
	int nquotes_set_property_sbyte(string name, char value);
	int nquotes_set_property_short(string name, short value);
	int nquotes_set_property_int(string name, int value);
	int nquotes_set_property_long(string name, long value);
	int nquotes_set_property_byte(string name, uchar value);
	int nquotes_set_property_ushort(string name, ushort value);
	int nquotes_set_property_uint(string name, uint value);
	int nquotes_set_property_ulong(string name, ulong value);
	int nquotes_set_property_float(string name, float value);
	int nquotes_set_property_double(string name, double value);
	int nquotes_set_property_datetime(string name, datetime value);
	int nquotes_set_property_color(string name, color value);
	int nquotes_set_property_string(string name, string value);
	int nquotes_set_property_adouble(string name, double& value[], int count=WHOLE_ARRAY, int start=0);

	bool nquotes_get_property_bool(string name);
	char nquotes_get_property_sbyte(string name);
	short nquotes_get_property_short(string name);
	int nquotes_get_property_int(string name);
	long nquotes_get_property_long(string name);
	uchar nquotes_get_property_byte(string name);
	ushort nquotes_get_property_ushort(string name);
	uint nquotes_get_property_uint(string name);
	ulong nquotes_get_property_ulong(string name);
	float nquotes_get_property_float(string name);
	double nquotes_get_property_double(string name);
	datetime nquotes_get_property_datetime(string name);
	color nquotes_get_property_color(string name);
	string nquotes_get_property_string(string name);
	int nquotes_get_property_array_size(string name);
	int nquotes_get_property_adouble(string name, double& value[]);
#import

int init()
{
	nquotes_setup("MetaQuotesSample.Bridge3", "Bridge");
	
	// insert your custom code here...
	Print("AlveoBridge version 3.1 started.");
	
	return (nquotes_init());
}

int start()
{
	return (nquotes_start());
}

int deinit()
{
	return (nquotes_deinit());
}

// optional event handlers:
// https://docs.mql4.com/basis/function/events
//double OnTester()
//{
//	return (nquotes_on_tester());
//}
void OnTimer()
{
	nquotes_on_timer();
}
//void OnChartEvent(const int id, const long& lparam, const double& dparam, const string& sparam)
//{
//	nquotes_on_chart_event(id, lparam, dparam, sparam);
//}
