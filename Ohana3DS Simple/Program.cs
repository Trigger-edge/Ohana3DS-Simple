using Ohana3DS_Rebirth.Ohana;
using System;

namespace Ohana3DS_Simple
{
    class Program
    {
        static void Main(string[] args)
        {
            //You can manually set the args here for debug/etc.
            //args = new string[] { "-e", "container.arc", "container"};
            try
            {
                object importedData = null;

                if (args.Length == 0)
                {
                    Console.WriteLine("Error: No args");
                }
                else
                {
                    int i = 0;
                    foreach (string arg in args)
                    {
                        if (arg[0] == '-')
                        {
                            if (arg[1] == 'e') //Export
                            {
                                string fileName = args[i + 1];
                                FileIO.fileType fileType = FileIO.strToFileType(args[i + 2]);

                                Console.WriteLine("Exporting: " + fileName);

                                importedData = FileIO.import(fileName);
                                FileIO.export(fileType, importedData, 0);
                            }
                            else
                            {
                                Console.WriteLine("Unknown arg: '-" + arg[1] + "'");
                            }
                        }
                        i++;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("");
                Console.WriteLine(e.Message);
            }
            Console.WriteLine("");
            //uncomment for better debugging
            //Console.ReadKey();
        }
    }
}
