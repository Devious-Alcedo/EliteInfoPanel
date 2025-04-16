using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliteInfoPanel.Util
{
    public static class VectorUtil
    {
        public static double CalculateDistance((double X, double Y, double Z) a, (double X, double Y, double Z) b)
        {
            return Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2) + Math.Pow(b.Z - a.Z, 2));
        }
    }

}
