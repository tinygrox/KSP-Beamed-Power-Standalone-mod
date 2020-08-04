# KSP-Beamed-Power-Standalone-mod
This is a mod (plugin) that adds Beamed Power code to the stock game (KSP)
This mod is currently licensed as Public Domain Work. This license only applies to the plugin.

There are two part modules this adds that you can use: WirelessSource and WirelessReceiverDirectional. 
At the moment, you'll have to add these part modules to any part you want to use for beamed power. Instructions for how to do that: 

WirelessSource:
If you want to make a part a beamed power transmitter, you need to add the WirelessSource part module to the part's config file. 
You can either directly add this to a part's config file, or use a Module Manager patch. It'll looks something like this: 

MODULE
{
	name = WirelessSource
	DishDiameter = 10	// in metres, pick a suitable value
	Wavelength = Long	// must be either 'Long'(short microwave) or 'Short'(short ultraviolet)
	Efficiency = 0.8	// value must be in range of 0.0 to 1.0
}


WirelessReceiverDirectional:

MODULE
{
	name = WirelessReceiverDirectional
	recvDiameter = 2.5	// in metres, pick an appropriate value
	recvEfficiency = 0.7	// a value between 0.0 and 1.0
}

If you want to make a part a beamed power receiver, you need to add the WirelessReceiverDirectional part module to the part's config file. 
You can either directly add this to a part's config file, or use a Module Manager patch. The syntax is shown above. 
