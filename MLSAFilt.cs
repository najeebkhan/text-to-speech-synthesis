using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTS
{
    class MLSAFilt
    {
        int M;
        int fp;
        double a;
        int pd;       
        static double[] ppade = { 1.0, 0.4999273, 0.1067005, 0.01170221, 0.0005656279 };
        
        double[] d;
        double[] pt1;
        double[] delay1;

        double[] pt2;
        double[][] delay2;
        //MLSAFilt(mfcorder=34, frameperiod=80, warping=0.42, padeorder=4)
        public MLSAFilt(int mfcorder, int frameperiod, double warping, int padeorder  )
        {
            M=mfcorder;
            fp=frameperiod;
            a=warping;
            pd=padeorder;

            d = new double[M + 2];
            pt1 = new double[pd + 1];
            delay1 = new double[pd + 1];

            pt2 = new double[pd + 1];
            delay2 = new double[pd + 1][];

            for (int i = 0; i <= pd; i++)
            {
                delay2[i] = new double[M + 2];
            }
        }

        public double[] mc2b(double[] mc, int M, double a)
        {
            double[] b = new double[M + 1];
            b[M] = mc[M];
            for (M--; M >= 0; M--)
                b[M] = mc[M] - a * b[M + 1];
            return b;
        }

        public double mlsadf(double x, double[] b)
        {
            x = mlsadf1(x, b);
            x = mlsadf2(x, b);

            return (x);
        }

        double mlsadf1(double x, double[] b)
        {
            double v, outp = 0.0, aa;
            int i;

            aa = 1 - a * a;

            for (i = pd; i >= 1; i--)
            {
                delay1[i] = aa * pt1[i - 1] + a * delay1[i];
                pt1[i] = delay1[i] * b[1];
                v = pt1[i] * ppade[i];

                x += (1 & i) == 1 ? v : -v;
                outp += v;
            }

            pt1[0] = x;
            outp += x;
            return (outp);


        }

        double mlsadf2(double x, double[] b)
        {
            double v, outp = 0.0;
            int i;

            //aa = 1 - a * a;
            for (i = pd; i >= 1; i--)
            {
                pt2[i] = mlsafir(pt2[i - 1], b, M, a, delay2[i]);
                v = pt2[i] * ppade[i];
                x += (1 & i) == 1 ? v : -v;
                outp += v;
            }
            pt2[0] = x;
            outp += x;
            return (outp);
        }

        double mlsafir(double x, double[] b, int m, double a, double[] del)
        {
            double y = 0.0, aa;
            int i;

            aa = 1 - a * a;

            del[0] = x;
            del[1] = aa * del[0] + a * del[1];
            //y = d[1] * b[1];
            for (i = 2; i <= m; i++)
            {
                del[i] = del[i] + a * (del[i + 1] - del[i - 1]);
                y += del[i] * b[i];
            }
            for (i = m + 1; i > 1; i--)
                del[i] = del[i - 1];

            return (y);
        }
        
    }
}
