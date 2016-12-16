using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTS
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("*************************************************************************");
            Console.WriteLine("******************************** UoU TTS ********************************");
            Console.WriteLine("*************************************************************************");

            string file = "twinkle";
            ParamGeneration.Text2Speech(file+".lab", file+".synth");
            
            Console.WriteLine("Done!");            
            Console.ReadKey();
        }       

    }
}
