﻿using Avalonia.Media;

namespace HandsLiftedApp.Utils
{
    public static class ColorHelper
    {
        /// <summary>
        /// Converts the HSL values to a Color.
        /// </summary>
        /// <param name="alpha">The alpha.</param>
        /// <param name="hue">The hue.</param>
        /// <param name="saturation">The saturation.</param>
        /// <param name="lighting">The lighting.</param>
        /// <returns></returns>
        public static Color FromHsl(int alpha, float hue, float saturation, float lighting)
        {
            if (0 > alpha || 255 < alpha)
            {
                throw new ArgumentOutOfRangeException("alpha");
            }
            if (0f > hue || 360f < hue)
            {
                throw new ArgumentOutOfRangeException("hue");
            }
            if (0f > saturation || 1f < saturation)
            {
                throw new ArgumentOutOfRangeException("saturation");
            }
            if (0f > lighting || 1f < lighting)
            {
                throw new ArgumentOutOfRangeException("lighting");
            }

            if (0 == saturation)
            {
                return Color.FromArgb(Convert.ToByte(alpha), Convert.ToByte(lighting * 255), Convert.ToByte(lighting * 255), Convert.ToByte(lighting * 255));
            }

            float fMax, fMid, fMin;
            int iSextant, iMax, iMid, iMin;

            if (0.5 < lighting)
            {
                fMax = lighting - (lighting * saturation) + saturation;
                fMin = lighting + (lighting * saturation) - saturation;
            }
            else
            {
                fMax = lighting + (lighting * saturation);
                fMin = lighting - (lighting * saturation);
            }

            iSextant = (int)Math.Floor(hue / 60f);
            if (300f <= hue)
            {
                hue -= 360f;
            }
            hue /= 60f;
            hue -= 2f * (float)Math.Floor(((iSextant + 1f) % 6f) / 2f);
            if (0 == iSextant % 2)
            {
                fMid = hue * (fMax - fMin) + fMin;
            }
            else
            {
                fMid = fMin - hue * (fMax - fMin);
            }

            iMax = Convert.ToInt32(fMax * 255);
            iMid = Convert.ToInt32(fMid * 255);
            iMin = Convert.ToInt32(fMin * 255);

            switch (iSextant)
            {
                case 1:
                    return Color.FromArgb(Convert.ToByte(alpha), Convert.ToByte(iMid), Convert.ToByte(iMax), Convert.ToByte(iMin));
                case 2:
                    return Color.FromArgb(Convert.ToByte(alpha), Convert.ToByte(iMin), Convert.ToByte(iMax), Convert.ToByte(iMid));
                case 3:
                    return Color.FromArgb(Convert.ToByte(alpha), Convert.ToByte(iMin), Convert.ToByte(iMid), Convert.ToByte(iMax));
                case 4:
                    return Color.FromArgb(Convert.ToByte(alpha), Convert.ToByte(iMid), Convert.ToByte(iMin), Convert.ToByte(iMax));
                case 5:
                    return Color.FromArgb(Convert.ToByte(alpha), Convert.ToByte(iMax), Convert.ToByte(iMin), Convert.ToByte(iMid));
                default:
                    return Color.FromArgb(Convert.ToByte(alpha), Convert.ToByte(iMax), Convert.ToByte(iMid), Convert.ToByte(iMin));
            }
        }
    }
}
