using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTS
{
    class Vocoder
    {

        public static float[] synthesize(double[][] mfc, double[] excit, int fp, int M, double a)
        {
            MLSAFilt mf = new MLSAFilt(mfcorder: M - 1, frameperiod: 80, warping: 0.42, padeorder: 4);
            float[] speech = new float[fp * mfc.GetLength(0)];
            double[] cl = mfc[0];
            double[] cr;
            cl = mf.mc2b(cl, M-1, a);
            double[] inc = new double[M + 1];

            for (int i = 0; i < mfc.GetLength(0)-1 ; i++)
            {
                cr = mfc[i];
                cr = mf.mc2b(cr, cr.Length - 1, a);

                for (int j = 0; j < cr.Length; j++)
                    inc[j] = (cr[j] - cl[j]) / fp;

                for (int j = 0; j < fp; j++)
                {
                    // mlsa here
                    speech[fp * i + j] = (float)(Math.Exp(cl[0]) * mf.mlsadf(excit[fp * i + j], cl));

                    for (int k = 0; k < cl.Length; k++)
                        cl[k] = cl[k] + inc[k];
                }
                for (int k = 0; k < cl.Length; k++)
                    cl[k] = cr[k];
            }
            return speech;
        }

        public static double[] excite(int[] p, int fp)
        {
            Random rand = new Random();
            double[] exci = new double[p.Length * fp];
            double p1 = p[0], pc = p[0], p2, inc;
            for (int i = 1; i < p.Length; i++)
            {
                p2 = p[i];
                if (p1 != 0 & p2 != 0)
                    inc = (p2 - p1) / fp;
                else
                { inc = 0; pc = p2; p1 = 0; }
                for (int j = 0; j < fp; j++)
                {
                    if (p1 == 0)
                        exci[fp * (i - 1) + j] = (float)Gausian(0, 1, rand);
                    else
                    {
                        pc++;
                        if (pc >= p1)
                        { exci[fp * (i - 1) + j] = (float)Math.Sqrt(p1); pc -= p1; }
                        else
                            exci[fp * (i - 1) + j] = 0;
                    }
                    p1 += inc;

                }
                p1 = p2;

            }
            return exci;
        }

        static double Gausian(double mean, double sigma, Random rand)
        {

            double u1 = rand.NextDouble(); //these are uniform(0,1) random doubles
            double u2 = rand.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                         Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
            return (mean + sigma * randStdNormal); //random normal(mean,stdDev^2)

        }

    }
}
