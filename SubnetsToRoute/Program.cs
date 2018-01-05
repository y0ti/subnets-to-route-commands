using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace SubnetsToRoute
{
    class Program
    {
        const string HELP_MESSAGE = 
@"This program converts file with list of subnets (like 172.17.9.0/24, one per line) to windows batch file with 'route add' commands.

Required parameters:
-i <file with subnets> - Input file
-if - Interface id (as listed by 'route print' command)
-gw - Gateway address
-m - Metric

Output parameters (at least one is required):
-of <output file name> - Output commands to the specified file
-oc - Output commands to console

Optional parameters:
-p - Add 'pause' after each 'route' command
";
        const string ROUTE_COMMAND = "route -p add {0} mask {1} {2} metric {3} if {4}";

        static string inputFileName = string.Empty;
        static string netIf = string.Empty;
        static string gateway = string.Empty;
        static string metric = string.Empty;

        static bool outToFile = false;
        static string outputFileName = string.Empty;
        static bool outToConsole = false;

        static bool addPause = false;

        static string networkAddress = string.Empty;
        static string networkMask = string.Empty;

        static void Main(string[] args)
        {
            if (!ParseArgs(args))
                return;
            StringBuilder commands = new StringBuilder();
            foreach (string subnet in File.ReadAllLines(inputFileName))
            {
                try
                {
                    string s = subnet.Trim();
                    s = s.Replace(" ", "");
                    string[] parts = s.Split('/');
                    string[] addressParts = parts[0].Split('.');
                    string[] calculated = CalculateSubnet(Int32.Parse(parts[1]), addressParts.Select(x => int.Parse(x)).ToArray());
                    networkAddress = calculated[0];
                    networkMask = calculated[1];
                }
                catch (Exception ex)
                {
                    ErrorMessage("Error while parsing line '{0}': {1}, ignoring...", subnet, ex.Message);
                }
                string command = String.Format(ROUTE_COMMAND, networkAddress, networkMask, gateway, metric, netIf);
                commands.AppendLine(command);
                if (outToConsole)
                    Console.WriteLine(command);
                if (addPause)
                    commands.AppendLine("pause");
            }

            try
            {
                if (outToFile)
                    File.WriteAllText(outputFileName, commands.ToString());
            }
            catch (Exception ex)
            {
                ErrorMessage("Can't write output file '{0}': {1}", outputFileName, ex.Message);
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine(HELP_MESSAGE);
        }

        static bool ParseArgs(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return false;
            }

            bool _i_set = false,
                _if_set = false,
                _gw_set = false,
                _m_set = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-i":
                        if (i != args.Length - 1 && File.Exists(args[i + 1]) && !args[i + 1].StartsWith("-"))
                        {
                            inputFileName = args[i + 1];
                            i++;
                            _i_set = true;
                        }
                        else
                        {
                            ErrorMessage("-i: wrong argument or file does not exist");
                            return false;
                        }
                        break;

                    case "-if":
                        if (i != args.Length - 1 && !args[i + 1].StartsWith("-"))
                        {
                            netIf = args[i + 1];
                            i++;
                            _if_set = true;
                        }
                        else
                        {
                            ErrorMessage("-if: wrong argument");
                            return false;
                        }
                        break;

                    case "-gw":
                        if (i != args.Length - 1 && !args[i + 1].StartsWith("-"))
                        {
                            gateway = args[i + 1];
                            i++;
                            _gw_set = true;
                        }
                        else
                        {
                            ErrorMessage("-gw: wrong argument");
                            return false;
                        }
                        break;

                    case "-m":
                        if (i != args.Length - 1 && !args[i + 1].StartsWith("-"))
                        {
                            metric = args[i + 1];
                            i++;
                            _m_set = true;
                        }
                        else
                        {
                            ErrorMessage("-m: wrong argument");
                            return false;
                        }
                        break;

                    case "-of":
                        if (i != args.Length - 1 && !args[i + 1].StartsWith("-") && !File.Exists(args[i + 1]))
                        {
                            outputFileName = args[i + 1];
                            i++;
                            outToFile = true;
                        }
                        else
                        {
                            ErrorMessage("-of: wrong argument or file already exists");
                            return false;
                        }
                        break;

                    case "-oc":
                        outToConsole = true;
                        break;

                    case "-p":
                        addPause = true;
                        break;

                    default:
                        ErrorMessage("Unknown parameter: {0}", args[i]);
                        break;
                }
            }
            if (_i_set && _if_set && _gw_set && _m_set && (outToConsole || outToFile))
                return true;
            else
                ErrorMessage("At least one of required parameters is missing");
            return false;
        }

        static void ErrorMessage(string formattedMessage, params string[] arg)
        {
            ConsoleColor foreground = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(formattedMessage, arg);
            Console.ForegroundColor = foreground;
        }

        static string[] CalculateSubnet(int subnet, params int[] addressParts)
        {
            return new string[] { String.Format("{0}.{1}.{2}.{3}", GetFirstAddressOfNetwork(subnet, addressParts).Cast<object>().ToArray()),
                                  String.Format("{0}.{1}.{2}.{3}", SubnetToNetmask(subnet).Cast<object>().ToArray())};
        }

        static bool[] AddressToBits(params int[] addressParts)
        {
            List<bool> bits = new List<bool>();
            foreach (int part in addressParts)
            {
                string s = Convert.ToString(part, 2);
                if (s.Length < 8)
                    s = s.PadLeft(8, '0');
                foreach (char c in s)
                {
                    if (c == '1')
                        bits.Add(true);
                    else bits.Add(false);
                }
            }
            return bits.ToArray();
        }

        static void SetLastBits(int numberOfBits, ref bool[] bits, bool setTo)
        {
            for (int i = bits.Length - numberOfBits; i < bits.Length; i++)
                bits[i] = setTo;
        }

        static int[] BitsToAddress(bool[] bits)
        {
            int[] addressParts = new int[4];
            for (int k = 0; k <= 3; k++)
            {
                string s = string.Empty;
                for (int i = 8 * k; i <= 8 * k + 7; i++)
                    s += bits[i] ? "1" : "0";
                addressParts[k] = Convert.ToInt32(s, 2);
            }

            return addressParts;
        }

        static int[] SubnetToNetmask(int subnet)
        {
            bool[] subnetOneBits = AddressToBits(255, 255, 255, 255);
            SetLastBits(32 - subnet, ref subnetOneBits, false);
            return BitsToAddress(subnetOneBits);
        }

        static int[] GetFirstAddressOfNetwork(int subnet, params int[] addressParts)
        {
            bool[] addressBits = AddressToBits(addressParts);
            SetLastBits(32 - subnet, ref addressBits, false);
            return BitsToAddress(addressBits);
        }
    }
}
