using System;

namespace DroneDDW.NarratorOrb.Rendering
{
	// three.js r184's colour maths, so palettes come out as in the preview: colours are linear, and hex values and HSL
	// are read and written in sRGB (ColorManagement SRGBToLinear and LinearToSRGB, Color.getHSL and Color.setHSL)
	internal static class NarratorOrbColorUtil
	{
		#region Internal

		internal static double SrgbToLinear(double value)
		{
			return value < 0.04045 ? value * 0.0773993808 : Math.Pow(value * 0.9478672986 + 0.0521327014, 2.4);
		}

		internal static double LinearToSrgb(double value)
		{
			return value < 0.0031308 ? value * 12.92 : 1.055 * Math.Pow(value, 0.41666) - 0.055;
		}

		// new THREE.Color(0x3050ff)
		internal static NarratorOrbRgb FromHex(int hex)
		{
			return FromSrgb(((hex >> 16) & 255) / 255.0, ((hex >> 8) & 255) / 255.0, (hex & 255) / 255.0);
		}

		internal static NarratorOrbRgb FromSrgb(double red, double green, double blue)
		{
			return new NarratorOrbRgb(SrgbToLinear(red), SrgbToLinear(green), SrgbToLinear(blue));
		}

		// Color.getHSL(target, SRGBColorSpace): the HSL of the colour's sRGB form, each 0 to 1
		internal static void ToHsl(NarratorOrbRgb color, out double hue, out double saturation, out double lightness)
		{
			double red = LinearToSrgb(color.R);
			double green = LinearToSrgb(color.G);
			double blue = LinearToSrgb(color.B);
			double max = Math.Max(red, Math.Max(green, blue));
			double min = Math.Min(red, Math.Min(green, blue));
			lightness = (min + max) / 2.0;
			if (min == max)
			{
				hue = 0;
				saturation = 0;
				return;
			}

			double delta = max - min;
			saturation = lightness <= 0.5 ? delta / (max + min) : delta / (2 - max - min);
			if (max == red)
			{
				hue = (green - blue) / delta + (green < blue ? 6 : 0);
			}
			else if (max == green)
			{
				hue = (blue - red) / delta + 2;
			}
			else
			{
				hue = (red - green) / delta + 4;
			}

			hue /= 6;
		}

		// Color.setHSL(h, s, l, SRGBColorSpace): the linear colour of an sRGB HSL
		internal static NarratorOrbRgb FromHsl(double hue, double saturation, double lightness)
		{
			hue = EuclideanModulo(hue, 1);
			saturation = Math.Max(0, Math.Min(1, saturation));
			lightness = Math.Max(0, Math.Min(1, lightness));
			if (saturation == 0)
			{
				return FromSrgb(lightness, lightness, lightness);
			}

			double high = lightness <= 0.5
				? lightness * (1 + saturation)
				: lightness + saturation - lightness * saturation;
			double low = 2 * lightness - high;
			return FromSrgb(HueToChannel(low, high, hue + 1.0 / 3), HueToChannel(low, high, hue),
				HueToChannel(low, high, hue - 1.0 / 3));
		}

		internal static double EuclideanModulo(double value, double modulus)
		{
			return (value % modulus + modulus) % modulus;
		}

		#endregion

		#region Private

		// hue2rgb(p, q, t), called with p the low value and q the high one
		private static double HueToChannel(double low, double high, double hue)
		{
			if (hue < 0)
			{
				hue += 1;
			}

			if (hue > 1)
			{
				hue -= 1;
			}

			if (hue < 1.0 / 6)
			{
				return low + (high - low) * 6 * hue;
			}

			if (hue < 1.0 / 2)
			{
				return high;
			}

			if (hue < 2.0 / 3)
			{
				return low + (high - low) * 6 * (2.0 / 3 - hue);
			}

			return low;
		}

		#endregion
	}
}
