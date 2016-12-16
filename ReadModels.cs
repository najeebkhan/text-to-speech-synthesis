using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace TTS
{
    class ReadModels
    {

        public static void ReadHTKFileAndSplit(string HTKMMFFileName, string mcrofile, string modelfile)
        {
            //read the mmf file
            string mmf = File.ReadAllText(HTKMMFFileName);
            //split into the macro part and hmm definition part
            int pos = mmf.IndexOf("~h");
            string macros = mmf.Substring(0, pos);
            string models = mmf.Substring(pos);
            File.WriteAllText(mcrofile, macros);
            File.WriteAllText(modelfile, models);
        }

        public static void ReadMacrosAndSerialize(string MacroFileName, string outputfile, bool dur)
        {
            Dictionary<string, stream> macrodic = new Dictionary<string, stream>();
            //read the macro file
            string macros = File.ReadAllText(MacroFileName);

            //Processing macro file
            stream tempstream;
            int pos = dur ? macros.IndexOf("~s") : macros.IndexOf("~p");
            int nextpos;
            while (pos != -1)
            {
                //Extract a single ~p macro in the string pmacro
                nextpos = dur ? macros.IndexOf("~s", pos + 2) : macros.IndexOf("~p", pos + 2);
                string pmacro = nextpos == -1 ? macros.Substring(pos) : macros.Substring(pos, nextpos - pos);

                //Extract the name of macro
                var match = dur ? Regex.Match(pmacro, @"~s "".*""") : Regex.Match(pmacro, @"~p "".*""");
                string name = match.Success ? match.Value.Substring(4, match.Length - 5) : "";
                
                // extract Stream number
                match = Regex.Match(pmacro, @"<STREAM> \d+");
                int strno = match.Success ? int.Parse(match.Value.Substring(9, match.Length - 9)) : -1;

                //extract the number of mixtures
                match = Regex.Match(pmacro, @"<NUMMIXES> \d+");
                int nummix = match.Success ? int.Parse(match.Value.Substring(11, match.Length - 11)) : 1;

                //extract the weights of each mixture
                double[] mixweight = new double[nummix];
                match = Regex.Match(pmacro, @"<MIXTURE> \d+ [-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?.");
                for (int i = 0; i < nummix & match.Success; i++)
                {
                    mixweight[i] = double.Parse(match.Value.Split(' ')[2]);
                    match = match.NextMatch();
                }

                //extract the vector size only from the mean of the first mixture in the stream
                match = Regex.Match(pmacro, @"<MEAN> \d+");
                int Vsize = match.Success ? int.Parse(match.Value.Substring(7, match.Length - 7)) : 0;

                //Extract the means for all the mixtures
                double[][] mean = new double[nummix][];
                match = Regex.Match(pmacro, @"<MEAN> \d+\s*([-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?\s*)+");
                for (int i = 0; i < nummix & match.Success; i++)
                {
                    string[] temp = match.Success ? match.Value.Split(' ') : null;
                    mean[i] = new double[Vsize];
                    for (int t = 0; t < mean[0].Length; t++)
                        mean[i][t] = double.Parse(temp[t + 2]);
                    match = match.NextMatch();
                }

                //Extract the variances for all the mixtures
                double[][] variance = new double[nummix][];
                match = Regex.Match(pmacro, @"<VARIANCE> \d+\s*([-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?\s*)+");
                for (int i = 0; i < nummix & match.Success; i++)
                {
                    string[] temp = match.Success ? match.Value.Split(' ') : null;
                    variance[i] = new double[Vsize];
                    for (int t = 0; t < variance[0].Length; t++)
                        variance[i][t] = 1/double.Parse(temp[t + 2]);
                    match = match.NextMatch();
                }

                //Extract the GConst for all the mixtures
                double[] gconst = new double[nummix];
                match = Regex.Match(pmacro, @"<GCONST> [-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?\s*");
                for (int i = 0; i < nummix & match.Success; i++)
                {
                    gconst[i] = double.Parse(match.Value.Split(' ')[1]);
                    match = match.NextMatch();
                }

                tempstream = new stream(nummix, Vsize);
                tempstream.MixtureWeights = mixweight.Length > 1 ? mixweight : null;
                tempstream.mean = mean;
                tempstream.variance = variance;
                tempstream.GConst = gconst;
                macrodic.Add(name, tempstream);
                // update current position
                pos = nextpos;
            }


            try
            {
                using (Stream strm = File.Open(outputfile, FileMode.Create))
                {
                    BinaryFormatter bin = new BinaryFormatter();
                    bin.Serialize(strm, macrodic);
                }
            }
            catch (IOException)
            {
                Console.WriteLine("Serialization Exception");
            }

            Console.WriteLine("Function ReadMacrosAndSerialize: Serialization Done!");
        }

        public static void ReadMacrosAndSerializeDur(string MacroFileName, string outputfile)
        {
            Dictionary<string, stream[]> macrodic = new Dictionary<string, stream[]>();

            //read the macro file
            string macros = File.ReadAllText(MacroFileName);

            //Processing macro file
            stream[] tempstream;
            int pos = macros.IndexOf("~s");
            int nextpos;
            while (pos != -1)
            {
                //Extract a single ~s macro in the string smacro
                nextpos = macros.IndexOf("~s", pos + 2);
                string smacro = nextpos == -1 ? macros.Substring(pos) : macros.Substring(pos, nextpos - pos);

                //Extract the name of macro
                var match = Regex.Match(smacro, @"~s "".*""");
                string name = match.Success ? match.Value.Substring(4, match.Length - 5) : "";
                string[] streams = smacro.Split(new string[] { "<STREAM>" }, StringSplitOptions.None);
                tempstream = new stream[streams.Length - 1];
                for (int s = 1; s < streams.Length; s++)
                {
                    //extract the number of mixtures
                    match = Regex.Match(streams[s], @"<NUMMIXES> \d+");
                    int nummix = match.Success ? int.Parse(match.Value.Substring(11, match.Length - 11)) : 1;

                    //extract the weights of each mixture
                    double[] mixweight = new double[nummix];
                    match = Regex.Match(streams[s], @"<MIXTURE> \d+ [-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?.");
                    for (int i = 0; i < nummix & match.Success; i++)
                    {
                        mixweight[i] = double.Parse(match.Value.Split(' ')[2]);
                        match = match.NextMatch();
                    }

                    //extract the vector size only from the mean of the first mixture in the stream
                    match = Regex.Match(streams[s], @"<MEAN> \d+");
                    int Vsize = match.Success ? int.Parse(match.Value.Substring(7, match.Length - 7)) : 0;

                    //Extract the means for all the mixtures
                    double[][] mean = new double[nummix][];
                    match = Regex.Match(streams[s], @"<MEAN> \d+\s*([-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?\s*)+");
                    for (int i = 0; i < nummix & match.Success; i++)
                    {
                        string[] temp = match.Success ? match.Value.Split(' ') : null;
                        mean[i] = new double[Vsize];
                        for (int t = 0; t < mean[0].Length; t++)
                            mean[i][t] = double.Parse(temp[t + 2]);
                        match = match.NextMatch();
                    }

                    //Extract the variances for all the mixtures
                    double[][] variance = new double[nummix][];
                    match = Regex.Match(streams[s], @"<VARIANCE> \d+\s*([-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?\s*)+");
                    for (int i = 0; i < nummix & match.Success; i++)
                    {
                        string[] temp = match.Success ? match.Value.Split(' ') : null;
                        variance[i] = new double[Vsize];
                        for (int t = 0; t < variance[0].Length; t++)
                            variance[i][t] = 1/double.Parse(temp[t + 2]);
                        match = match.NextMatch();
                    }

                    //Extract the GConst for all the mixtures
                    double[] gconst = new double[nummix];
                    match = Regex.Match(streams[s], @"<GCONST> [-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?\s*");
                    for (int i = 0; i < nummix & match.Success; i++)
                    {
                        gconst[i] = double.Parse(match.Value.Split(' ')[1]);
                        match = match.NextMatch();
                    }

                    tempstream[s - 1] = new stream(nummix, Vsize);
                    tempstream[s - 1].MixtureWeights = mixweight.Length > 1 ? mixweight : null;
                    tempstream[s - 1].mean = mean;
                    tempstream[s - 1].variance = variance;
                    tempstream[s - 1].GConst = gconst;
                }

                //if (nextpos < 1000)
                //    Console.WriteLine(streams[4]);
                macrodic.Add(name, tempstream);
                // update current position
                pos = nextpos;
            }


            try
            {
                using (Stream strm = File.Open(outputfile, FileMode.Create))
                {
                    BinaryFormatter bin = new BinaryFormatter();
                    bin.Serialize(strm, macrodic);
                }
            }
            catch (IOException)
            {
                Console.WriteLine("Serialization Exception");
            }

            Console.WriteLine("Function ReadMacrosAndSerializeDur: Serialization Done!");
        }

        public static void ReadHMMDefinitionsAndSerializeDur(Dictionary<string, stream[]> macrodic, string modelfile, string serializedfile)
        {
            Dictionary<string, model> modeldic = new Dictionary<string, model>();

            //read the macro file
            string macros = File.ReadAllText(modelfile);

            int NStreams = 4;
            //Processing macro file
            model tempmodel;
            stream[] tempstreams;
            int pos = macros.IndexOf("~h");
            int nextpos;
            while (pos != -1)
            {
                nextpos = macros.IndexOf("~h", pos + 2);
                string hmacro = nextpos == -1 ? macros.Substring(pos) : macros.Substring(pos, nextpos - pos);

                //Extract the name of hmm
                var match = Regex.Match(hmacro, @"~h "".*""");
                string name = match.Success ? match.Value.Substring(4, match.Length - 5) : "";

                //Extract Number of States in the model
                match = Regex.Match(hmacro, @"<NUMSTATES> \d+");
                int NStates = match.Success ? int.Parse(match.Value.Substring(12, match.Length - 12)) : -1;

                state[] tempstates = new state[1];
                tempstates[0] = new state(NStreams);
                //Extract the state
                tempstreams = new stream[NStreams];
                //Extract the streams
                match = Regex.Match(hmacro, @"~s "".*""");
                string strm = match.Value.Substring(4, match.Value.Length - 5);
                tempstreams = macrodic[strm];

                tempstates[0] = new state(NStreams);
                tempstates[0].streams = tempstreams;

                tempmodel = new model(NStreams);
                tempmodel.states = tempstates;
                modeldic.Add(name, tempmodel);

                // update current position
                pos = nextpos;
            }

            try
            {
                using (Stream strm = File.Open(serializedfile, FileMode.Create))
                {
                    BinaryFormatter bin = new BinaryFormatter();
                    bin.Serialize(strm, modeldic);
                }
            }
            catch (IOException)
            {
                Console.WriteLine("Serialization Exception");
            }

            Console.WriteLine("Function ReadHMMDefinitionsAndSerializeDur: Serialization Done!");

        }

        public static Dictionary<string, stream> DeserializeMacros(string macrofile)
        {
            Dictionary<string, stream> macrodic = null;
            try
            {
                using (Stream strm = File.Open(macrofile, FileMode.Open))
                {
                    BinaryFormatter bin = new BinaryFormatter();
                    macrodic = (Dictionary<string, stream>)bin.Deserialize(strm);

                }
            }
            catch (IOException)
            {
                Console.WriteLine("Deserialization Error");
                return null;
            }
            return macrodic;
        }

        public static void ReadHMMDefinitionsAndSerialize(Dictionary<string, stream> macrodic, string modelfile, string serializedfile, bool dur)
        {
            Dictionary<string, model> modeldic = new Dictionary<string, model>();

            //read the macro file
            string macros = File.ReadAllText(modelfile);

            int NStreams = 4;
            //Processing macro file
            model tempmodel;
            state[] tempstates;
            stream[] tempstreams;
            int pos = macros.IndexOf("~h");
            int nextpos;
            while (pos != -1)
            {
                nextpos = macros.IndexOf("~h", pos + 2);
                string hmacro = nextpos == -1 ? macros.Substring(pos) : macros.Substring(pos, nextpos - pos);

                //Extract the name of hmm
                var match = Regex.Match(hmacro, @"~h "".*""");
                string name = match.Success ? match.Value.Substring(4, match.Length - 5) : "";

                //Extract Number of States in the model
                match = Regex.Match(hmacro, @"<NUMSTATES> \d+");
                int NStates = match.Success ? int.Parse(match.Value.Substring(12, match.Length - 12)) : -1;

                tempstates = new state[NStates];
                //Extract the states
                string[] states = hmacro.Split(new string[] { "<STATE>" }, StringSplitOptions.None);
                //the first split has no state information
                for (int j = 1; j < states.Length; j++)
                {
                    tempstreams = new stream[NStreams];
                    //Extract the streams
                    match = dur ? Regex.Match(states[j], @"~s "".*""") : Regex.Match(states[j], @"~p "".*""");
                    for (int i = 0; i < NStreams & match.Success; i++)
                    {
                        string strm = match.Value.Substring(4, match.Value.Length - 5);
                        tempstreams[i] = macrodic[strm];
                        match = match.NextMatch();
                    }
                    tempstates[j - 1] = new state(NStreams);
                    tempstates[j - 1].streams = tempstreams;
                }
                tempmodel = new model(NStreams);
                tempmodel.states = tempstates;
                modeldic.Add(name, tempmodel);

                // update current position
                pos = nextpos;
            }

            try
            {
                using (Stream strm = File.Open(serializedfile, FileMode.Create))
                {
                    BinaryFormatter bin = new BinaryFormatter();
                    bin.Serialize(strm, modeldic);
                }
            }
            catch (IOException)
            {
                Console.WriteLine("Serialization Exception");
            }

            Console.WriteLine("Function ReadHMMDefinitionsAndSerialize: Serialization Done!");

        }

        public static Dictionary<string, model> DeserializeModels(string modelfile)
        {
            Dictionary<string, model> modeldic = null;
            try
            {
                using (Stream strm = File.Open(modelfile, FileMode.Open))
                {
                    BinaryFormatter bin = new BinaryFormatter();
                    modeldic = (Dictionary<string, model>)bin.Deserialize(strm);

                }
            }
            catch (IOException)
            {
                Console.WriteLine("Deserialization Error");
                return null;
            }
            return modeldic;
        }

        public static Dictionary<string, stream[]> DeserializeMacrosDur(string macrofile)
        {
            Dictionary<string, stream[]> macrodic = null;
            try
            {
                using (Stream strm = File.Open(macrofile, FileMode.Open))
                {
                    BinaryFormatter bin = new BinaryFormatter();
                    macrodic = (Dictionary<string, stream[]>)bin.Deserialize(strm);

                }
            }
            catch (IOException)
            {
                Console.WriteLine("Deserialization Error");
                return null;
            }
            return macrodic;
        }
        


    }
}
