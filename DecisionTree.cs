using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
namespace TTS
{

    class DurDecisionTree
    {
        Dictionary<string, string[]> Questions = new Dictionary<string, string[]>();
        public List<Node> Tree = new List<Node>();

        public DurDecisionTree()
        {
            //deserialization here
        }

        public DurDecisionTree(string treefile)
        {

            string allQues = File.ReadAllText(treefile);
            string[] streams = allQues.Split(new string[] { "{*}" }, StringSplitOptions.RemoveEmptyEntries);

            //Read the questions... One question per line
            string[] ques = streams[0].Split('\n');                    

            // Add the questions to the Questions dictionary
            //due to ^ and + being special characters in REGEX, they are replaces with & and !
            for (int q = 0; q < ques.Length && ques[q].StartsWith("QS"); q++)
                Questions.Add(ques[q].Substring(3, ques[q].IndexOf("{") - 4), ques[q].Substring(ques[q].IndexOf("{")).Replace("{ ", "").Replace("}", "").Replace("\"", "").Replace("+", "!").Replace("^", "&").Replace("*", ".").Replace(" ", "").Split(','));

            string[] tree = streams[1].Split('\n');
            for (int n = 0; n < tree.Length - 1; n++)
            {
                tree[n] = Regex.Replace(tree[n], @"\s+", " ");
                if (!Regex.Match(tree[n], @"^ -?\d+").Success)
                    continue;
                string[] parts = tree[n].Trim().Split(null);
                Tree.Add(new Node(parts[1], parts[3], parts[2]));
            }
            //serialization here
        }

        public Node GetLeaf(string phone, Node n, bool trace=false)
        {
            //Debuging 
            if(trace)
                Console.WriteLine("{0} ? {1}", n.Name, Check(phone, Questions[n.Name]));

            if ((Check(phone, Questions[n.Name]) ? n.Positive : n.Negative).Contains("\""))
                return n;
            return Check(phone, Questions[n.Name]) ? GetLeaf(phone, Tree[Math.Abs(int.Parse(n.Positive))], trace) : GetLeaf(phone, Tree[Math.Abs(int.Parse(n.Negative))], trace);
        }

        static bool Check(string s, string[] patterns)
        {
            for (int i = 0; i < patterns.Length; i++)
            {
                if (Regex.Match(s, patterns[i]).Success)
                    return true;

            }
            return false;
        }

        public string Leaf(string phone,bool trace=false)
        {
            phone = phone.Replace("+", "!").Replace("^", "&");
            Node n = GetLeaf(phone, Tree[0],trace);
            //Debuging 
            if (trace)
                Console.WriteLine("{0} ? {1} ", n.Name, Check(phone, Questions[n.Name]));
            return (Check(phone, Questions[n.Name]) ? n.Positive : n.Negative);
        }


    }

    class CmpDecisionTree
    {
        

        Dictionary<string, string[]> Questions = new Dictionary<string, string[]>();
        //public List<Node> Tree = new List<Node>();
        public List<Node>[] Tree;
        public CmpDecisionTree()
        {
            //deserialization here
        }

        public CmpDecisionTree(string treefile)
        {
            string allQues = File.ReadAllText(treefile);
            string[] states = allQues.Split(new string[] { "{*}" }, StringSplitOptions.RemoveEmptyEntries);

            //Read the questions... One question per line
            string[] ques = File.ReadAllLines(treefile);
            // Add the questions to the Questions dictionary
            //due to ^ and + being special characters in REGEX, they are replaces with & and !
            for (int q = 0; q < ques.Length && ques[q].StartsWith("QS"); q++)
                Questions.Add(ques[q].Substring(3, ques[q].IndexOf("{") - 4), ques[q].Substring(ques[q].IndexOf("{")).Replace("{ ", "").Replace("}", "").Replace("\"", "").Replace("+", "!").Replace("^", "&").Replace("*", ".").Replace(" ", "").Split(','));


            Tree = new List<Node>[states.Length];
            for (int i = 1; i < states.Length; i++)
            {
                Tree[i] = new List<Node>();
                string[] tree = states[i].Split('\n');
                for (int n = 0; n < tree.Length - 1; n++)
                {
                    tree[n] = Regex.Replace(tree[n], @"\s+", " ");
                    if (!Regex.Match(tree[n], @"^ -?\d+").Success)
                        continue;
                    string[] parts = tree[n].Trim().Split(null);
                    Tree[i].Add(new Node(parts[1], parts[3], parts[2]));
                }
            }

            //serialization here
        }

        public Node GetLeaf(string phone, Node n, int stat, bool trace=false)
        {
            //Debuging 
            if(trace)
                Console.WriteLine("{0} ? {1}", n.Name, Check(phone, Questions[n.Name]));

            if ((Check(phone, Questions[n.Name]) ? n.Positive : n.Negative).Contains("_"))
                return n;
            return Check(phone, Questions[n.Name]) ? GetLeaf(phone, Tree[stat][Math.Abs(int.Parse(n.Positive))], stat,trace) : GetLeaf(phone, Tree[stat][Math.Abs(int.Parse(n.Negative))], stat,trace);
        }

        static bool Check(string s, string[] patterns)
        {
            for (int i = 0; i < patterns.Length; i++)
            {
                if (Regex.Match(s, patterns[i]).Success)
                    return true;

            }
            return false;
        }

        public string Leaf(string phone, int stat, bool trace=false)
        {
            phone = phone.Replace("+", "!").Replace("^", "&");
            Node n = GetLeaf(phone, Tree[stat][0], stat,trace);
            //Debuging 
            if (trace)
                Console.WriteLine("{0} ? {1}", n.Name, Check(phone, Questions[n.Name]));

            return Check(phone, Questions[n.Name]) ? n.Positive : n.Negative;
        }
    }

    class Node
    {
        public Node(string name, string pos, string neg)
        {
            Name = name;
            Positive = pos;
            Negative = neg;
        }
        public string Name      { get; set; }
        public string Positive  { get; set; }        
        public string Negative  { get; set; }       
    }

}
