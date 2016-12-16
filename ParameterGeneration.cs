using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace TTS
{

    class ParamGeneration
    {
        //First of all load the model definitions
        Dictionary<string, model> CMPmodels;
        Dictionary<string, model> DURmodels;
        public Dictionary<string, stream> CMPmacrodic;
        public Dictionary<string, stream[]> DURmacrodic;
        double TotalDur = 0;
        string[] modelNames;
        int NUMSTATES;
        double[][] c = new double[2][];
        double[][] wuw;
        double[] wum;
        double[][] M;
        double[][] U;
        double[][] param;
        int BW;
        int order;
        int num;
        int[][] width = new int[2][];
        const double epsilon=1E-20;

        //decision tree 
        public DurDecisionTree ddt = new DurDecisionTree("dur.inf.untied");
        public CmpDecisionTree cdt = new CmpDecisionTree("mgc.inf.untied");
        public CmpDecisionTree ldt = new CmpDecisionTree("lf0.inf.untied");
                    
        public ParamGeneration(int order, int NUMSTATES, int BW)
        {
            this.order = order;
            this.NUMSTATES = NUMSTATES;
            this.BW = BW;
            num = 3;
            c[0] = new double[] { -0.5, 0, 0.5 };
            c[1] = new double[] { 0, 1, -2, 1, 0 };

            width[0] = new int[] { -1, 1 };
            width[1] = new int[] { -2, 2 };           
            
            ////CMP file processing
            ReadModels.ReadHTKFileAndSplit("cmp.mmf", "CMPmacro.txt", "CMPmodel.txt");
            ReadModels.ReadMacrosAndSerialize("CMPmacro.txt", "CMPmacro.bin", false);
            CMPmacrodic = ReadModels.DeserializeMacros("CMPmacro.bin");
            ReadModels.ReadHMMDefinitionsAndSerialize(CMPmacrodic, "CMPmodel.txt", "CMPmodel.bin", false);

            //DUR file processing
            ReadModels.ReadHTKFileAndSplit("dur.mmf", "DURmacro.txt", "DURmodel.txt");
            ReadModels.ReadMacrosAndSerializeDur("DURmacro.txt", "DURmacro.bin");
            DURmacrodic = ReadModels.DeserializeMacrosDur("DURmacro.bin");
            ReadModels.ReadHMMDefinitionsAndSerializeDur(DURmacrodic, "DURmodel.txt", "DURmodel.bin");

            CMPmodels = ReadModels.DeserializeModels("CMPmodel.bin");
            DURmodels = ReadModels.DeserializeModels("DURmodel.bin");
            
        }

        double[][] chol(double[][] W)
        {
            int T = W.Length;
            int BW = W[0].Length;
            for (int t = 1; t <= T; t++)
            {
                for (int i = 1; t - i > 0 && i < BW; i++)
                    W[t - 1][0] -= W[t - i - 1][i] * W[t - i - 1][i];

                W[t - 1][0] = Math.Sqrt(W[t - 1][0]);

                for (int i = 2; i <= BW; i++)
                {
                    for (int j = 1; t - j > 0 && j <= BW - i; j++)
                        W[t - 1][i - 1] -= W[t - j - 1][j] * W[t - j - 1][i + j - 1];
                    W[t - 1][i - 1] /= W[t - 1][0];
                }
            }
            return W;
        }

        double[] ForwardSub(double[][] W, double[] M)
        {
            int T = W.Length;
            int BW = W[0].Length;
            double[] g = new double[T];
            g[0] = M[0] / W[0][0];
            for (int t = 1; t < T; t++)
            {
                double h = 0;
                for (int i = 1; i < BW - 1 && t - i >= 0; i++)
                    h += W[t - i][i] * g[t - i];
                g[t] = (M[t] - h) / W[t][0];
            }


            return g;
        }

        double[] BackwardSub(double[][] W, double[] g)
        {
            int T = W.Length;
            int BW = W[0].Length;
            double[] c = new double[T];
            c[T - 1] = g[T - 1] / W[T - 1][0];
            for (int t = T - 2; t >= 0; t--)
            {
                double h = 0;
                for (int i = 1; i < BW - 1 && t + i < T; i++)
                    h += W[t][i] * c[t + i];
                c[t] = (g[t] - h) / W[t][0];
            }
            return c;
        }

        void BackwardSub(double[][] W, double[] g, int m)
        {
            int T = W.Length;
            int BW = W[0].Length;

            param[T - 1][m] = g[T - 1] / W[T - 1][0];
            for (int t = T - 2; t >= 0; t--)
            {
                double h = 0;
                for (int i = 1; i < BW - 1 && t + i < T; i++)
                    h += W[t][i] * param[t + i][m];
                param[t][m] = (g[t] - h) / W[t][0];
            }

        }

        double coef(int i, int j) { return c[i - 1][i + j]; }

        void WUM_WUW1(double[][] M, double[][] U, int m)
        {
            int T = U.GetLength(0);

            wuw = new double[T][];
            for (int i = 0; i < T; i++)
                wuw[i] = new double[BW];
            wum = new double[T];

            for (int t = 1; t <= T; t++)
            {
                wum[-1 + t] = U[-1 + t][ m] * M[-1 + t][ m];
                wuw[-1 + t][-1 + 1] = U[-1 + t][ m];
                for (int i = 1; i < num; i++)
                {
                    for (int j = width[-1 + i][0]; j <= width[-1 + i][1]; j++)
                    {
                        if (t + j > 0 && t + j <= T && coef(i, -j) != 0)
                        {
                            double wu = coef(i, -j) * U[-1 + t + j][ i * order + m];
                            wum[-1 + t] += wu * M[-1 + t + j][ i * order + m];
                            for (int k = 0; k < BW; k++)
                                if (k - j <= width[-1 + i][1] && t + k <= T && coef(i, k - j) != 0)
                                    wuw[-1 + t][-1 + k + 1] = wuw[-1 + t][-1 + k + 1] + wu * coef(i, k - j);
                        }
                    }
                }
            }
        }

        bool CheckBoundary(bool[] voiced,int t )
        {
            for (int i = 1; i < 3;i++ )                          
                for (int j = width[i-1][0]; j <= width[i-1][1]; j++)                
                    if(coef(i,j) !=0 && (t+j)>=0 && (t+j)<voiced.Length && voiced[t+j]==false)
                        return true;
            return false;
        }

        public double[][] pdf2par()
        {
            for (int m = 0; m < order; m++)
            {
                WUM_WUW1(M, U, m);
                double[][] C = chol(wuw);
                BackwardSub(C, ForwardSub(C, wum), m);
            }
            return param;
        }

        public double[][] Model2Param(string modelfile)
        {                   
            modelNames = File.ReadAllText(modelfile).Split('\n');            
            
            for (int i = 0; i < modelNames.Length; i++)
            {
                if (modelNames[i] == "")
                    continue;
                for (int j = 0; j < DURmodels[modelNames[i]].states[0].streams.Length; j++)
                    TotalDur += (Math.Round(DURmodels[modelNames[i]].states[0].streams[j].mean[0][0]));
            }
            M = new double[(int)(TotalDur)][];
            U = new double[(int)(TotalDur)][];

            param = new double[U.GetLength(0)][];
            for (int i = 0; i < U.GetLength(0); i++)
                param[i] = new double[order];

            int f=0;
            for (int i = 0; i < modelNames.Length; i++)
            {
                for (int j = 0; j < NUMSTATES; j++)
                {
                    if (modelNames[i] == "")
                        continue;
                    if (Math.Round(DURmodels[modelNames[i]].states[0].streams[j].mean[0][0]) < 1)
                        Console.WriteLine("State {0} ignored in model {1}", j, modelNames[i]);
                    for (int k = 0; k < Math.Round(DURmodels[modelNames[i]].states[0].streams[j].mean[0][0]); k++)
                    {
                        M[f] = CMPmodels[modelNames[i]].states[j].streams[0].mean[0];
                        U[f] = CMPmodels[modelNames[i]].states[j].streams[0].variance[0];
                        f++;                     
                    }
                }                     
            }           
            return pdf2par();
        }
        
        public int[] Model2Pitch(int samplingRate)
        {
            order = 1;
            int vframe = 0;
            bool[] voiced = new bool[(int)TotalDur];
            bool v;
            int g = 0;

            //calculate number of voiced frames
            for (int i = 0; i < modelNames.Length; i++)
            {
                for (int j = 0; j < NUMSTATES; j++)
                {
                    if (modelNames[i] == "")
                        continue;
                    v = (CMPmodels[modelNames[i]].states[j].streams[1].MixtureWeights[0] >= 0.4);
                    for (int k = 0; k < Math.Round(DURmodels[modelNames[i]].states[0].streams[j].mean[0][0]); k++)
                    {
                        if (v)
                        {
                            voiced[g] = v;
                            vframe++;
                        }
                        g++;
                    }
                }
            }


            //Only for voiced frames
            M = new double[vframe][];
            U = new double[vframe][];
            param = new double[U.GetLength(0)][];
            for (int i = 0; i < param.GetLength(0); i++)
                param[i] = new double[order];

            int f = 0;
            g = 0;
            for (int i = 0; i < modelNames.Length; i++)
            {
                for (int j = 0; j < NUMSTATES; j++)
                {
                    if (modelNames[i] == "")
                        continue;
                    for (int k = 0; k < Math.Round(DURmodels[modelNames[i]].states[0].streams[j].mean[0][0]); k++)
                    {
                        if (voiced[g])
                        {
                            //voiced stream 1,2,3, mixture 0
                            M[f] = new double[3] {  CMPmodels[modelNames[i]].states[j].streams[1].mean[0][0],
                                                    CMPmodels[modelNames[i]].states[j].streams[2].mean[0][0],
                                                    CMPmodels[modelNames[i]].states[j].streams[3].mean[0][0]};
                            U[f] = new double[3] {  CMPmodels[modelNames[i]].states[j].streams[1].variance[0][0],
                                                    CMPmodels[modelNames[i]].states[j].streams[2].variance[0][0],
                                                    CMPmodels[modelNames[i]].states[j].streams[3].variance[0][0]};

                            if (CheckBoundary(voiced, g))
                            {
                                U[f] = new double[3] { epsilon, epsilon, epsilon };
                            }

                            f++;
                        }
                        g++;
                    }
                }
            }



            int[] vpitch = LogF02pitch(pdf2par(), samplingRate);
            int[] pitch = new int[(int)TotalDur];
            for (g = 0, f = 0; g < pitch.Length; g++)
            {
                if (voiced[g])
                {
                    pitch[g] = vpitch[f];
                    f++;
                }
                else
                    pitch[g] = 0;
            }
            
            return pitch;
        }
      
        public int[] LogF02pitch(double[][] lf0,int samplingRate)
        {
            int[] pitch = new int[lf0.Length];
            for(int i=0; i<pitch.Length; i++)
                pitch[i] = (int)(samplingRate * Math.Exp(-lf0[i][0]));
            return pitch;
        }
        
        public double[][] Model2Param1(string modelfile)
        {
            modelNames = File.ReadAllText(modelfile).Split('\n');
            for (int i = 0; i < modelNames.Length; i++)
            {
                if (modelNames[i] == "")
                    continue;
                //get the 
                for (int j = 0; j < NUMSTATES; j++)
                    TotalDur += (Math.Round(GetDurStream(modelNames[i], j).mean[0][0]));
            }
            M = new double[(int)(TotalDur)][];
            U = new double[(int)(TotalDur)][];

            param = new double[U.GetLength(0)][];
            for (int i = 0; i < U.GetLength(0); i++)
                param[i] = new double[order];

            int f = 0;
            for (int i = 0; i < modelNames.Length; i++)
            {
                for (int j = 0; j < NUMSTATES; j++)
                {
                    if (modelNames[i] == "")
                        continue;
                    bool trace = false;
                    if (i == 0 && j == 0) trace = true;
                    if (Math.Round(GetDurStream(modelNames[i], j,trace).mean[0][0]) < 1)
                        Console.WriteLine("State {0} ignored in model {1}", j, modelNames[i]);
                    for (int k = 0; k < Math.Round(GetDurStream(modelNames[i], j).mean[0][0]); k++)
                    {
                        trace = false;
                        if (i == 0 && j==0 && k==0) trace = true;
                        M[f] = GetStream(modelNames[i], j+1, 1,trace).mean[0];
                        U[f] = GetStream(modelNames[i], j+1, 1).variance[0];
                        f++;
                    }
                }
            }
            return pdf2par();
        }
        
        public int[] Model2Pitch1(int samplingRate)
        {
            order = 1;
            int vframe = 0;
            bool[] voiced = new bool[(int)TotalDur];
            bool v;
            int g = 0;

            //calculate number of voiced frames
            for (int i = 0; i < modelNames.Length; i++)
            {
                for (int j = 0; j < NUMSTATES; j++)
                {
                    if (modelNames[i] == "")
                        continue;                   
                   
                    v = (GetStream(modelNames[i], j+1, 2).MixtureWeights[0] > 0.5); //check the first voiced stream                    
                    for (int k = 0; k < Math.Round(GetDurStream(modelNames[i], j).mean[0][0]); k++)
                    {
                        if (v)
                        {
                            voiced[g] = v;
                            vframe++;
                        }
                        g++;
                    }
                }
            }


            //Only for voiced frames
            M = new double[vframe][];
            U = new double[vframe][];
            param = new double[U.GetLength(0)][];
            for (int i = 0; i < param.GetLength(0); i++)
                param[i] = new double[order];

            int f = 0;
            g = 0;
            for (int i = 0; i < modelNames.Length; i++)
            {
                for (int j = 0; j < NUMSTATES; j++)
                {
                    if (modelNames[i] == "")
                        continue;
                    for (int k = 0; k < Math.Round(GetDurStream(modelNames[i], j).mean[0][0]); k++)
                    {
                        if (voiced[g])
                        {
                            //voiced stream 1,2,3, mixture 0
                            M[f] = new double[3] {  GetStream(modelNames[i], j+1, 2).mean[0][0],
                                                    GetStream(modelNames[i], j+1, 3).mean[0][0],
                                                    GetStream(modelNames[i], j+1, 4).mean[0][0]};
                            U[f] = new double[3] {  GetStream(modelNames[i], j+1, 2).variance[0][0],
                                                    GetStream(modelNames[i], j+1, 3).variance[0][0],
                                                    GetStream(modelNames[i], j+1, 4).variance[0][0]};

                            if (CheckBoundary(voiced, g))
                            {
                                U[f] = new double[3] { epsilon, epsilon, epsilon };
                            }

                            f++;
                        }
                        g++;
                    }
                }
            }



            int[] vpitch = LogF02pitch(pdf2par(), samplingRate);
            int[] pitch = new int[(int)TotalDur];
            for (g = 0, f = 0; g < pitch.Length; g++)
            {
                if (voiced[g])
                {
                    pitch[g] = vpitch[f];
                    f++;
                }
                else
                    pitch[g] = 0;
            }

            return pitch;
        }
      
        public stream GetStream(string phone, int stat, int strm, bool trace=false)
        {
            string key = strm == 1 ? cdt.Leaf(phone, stat, trace).Replace("\"", "") : (ldt.Leaf(phone, stat).Replace("\"", "")) + "-" + strm.ToString();
            if (trace && strm == 1)
                Console.WriteLine("Phone: {0}, stream macro: {1}", phone.Trim(), key);
            return strm == 1 ? CMPmacrodic[key] : CMPmacrodic[key];
        }

        public stream GetDurStream(string phone, int strm,bool trace=false)
        {
            string macroName = ddt.Leaf(phone,trace).Replace("\"", "");
            if (trace)
                Console.WriteLine("Phone: {0}, stream macro: {1}\n\n", phone.Trim(), macroName);
            return DURmacrodic[macroName][strm];
            
        }

        public static void Text2Speech(string textfile, string speechfile)
        {
            int samplingRate = 16000;
            int M = 35;
            int fp = 80;
            double a = 0.42;

            #region parameter Generation

            ParamGeneration pg = new ParamGeneration(M, 3, 5);
            //generate mfcc
            double[][] mfc = pg.Model2Param1(textfile);
            //generate pitch
            int[] pitch = pg.Model2Pitch1(samplingRate);

            #endregion

            #region Synthesis

            //convert to excitation
            double[] exci = Vocoder.excite(pitch, fp);
            float[] synth = Vocoder.synthesize(mfc, exci, fp, M, a);

            using (BinaryWriter b = new BinaryWriter(File.Open(speechfile, FileMode.Create)))
            {
                // write shorts to file
                for (int i = 0; i < synth.Length; i++)
                    b.Write((Int16)(synth[i]));
            }

            #endregion            

            pg.Debug();
            

        }

        public void Debug()
        {
            for (int i = 0; i < 40; i++)
                Console.WriteLine("{0} node: {1} ? yes: {2}, no: {3} ", i, cdt.Tree[1][i].Name, cdt.Tree[1][i].Positive, cdt.Tree[1][i].Negative);
        }

    }
    
}
