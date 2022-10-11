﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace Ada
{
    class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                var dirRegex = new Regex(@"alias\s+(?'name'\w+)\s*=\s*'cd\s+('value'\w+)'", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                if (args.Length == 0)
                {
                    PrintUsageAndExit();
                }
                var normalArgs = args.Where(x => x[0] != '-').ToArray();
                var optionArgs = args.Where(x => x[0] == '-').SelectMany(x=>x.Skip(1)).ToHashSet();

                var folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var fullPathToAliasFile = Path.Combine(folder, ".dir-aliases");

                var aliasList = new List<DirectoryAlias>();
                int lineNumber = 1;
                foreach (var line in File.ReadLines(fullPathToAliasFile).Select(x=>x.Trim()))
                {
                    if (line == string.Empty)
                    {
                        continue;
                    }
                    if (line.StartsWith("#"))
                    {
                        continue;
                    }
                    var match = dirRegex.Match(line);
                    if (!match.Success)
                    {
                        Console.Error.WriteLine($"Warning alias on line {lineNumber} of file {fullPathToAliasFile} is invalid.");
                        continue;
                    }

                    aliasList.Add(new DirectoryAlias
                    {
                        Alias = match.Groups["name"].Value,
                        Directory = match.Groups["value"].Value,
                    });
                }

                var replace = optionArgs.Contains('r');
                var deleteOne = optionArgs.Contains('d');
                var add = optionArgs.Contains('a');
                var printThis = optionArgs.Contains('t');

                var r = new[] { replace, deleteOne, add, printThis }.ToLookup(x => x);
                if (r.Count() > 1)
                {
                    throw new Exception("Only one of -r, -a, -d or -t can be supplied.")
                }

                if (replace)
                {
                    if (normalArgs.Length == 0)
                    {
                        throw new Exception("You must supply an alias to replace.");
                    }
                    var alias = normalArgs[0];
                    var lk = aliasList.ToLookup(x => x.Alias);
                    var toReplace = lk[alias];
                    if (toReplace.Count() == 0)
                    {
                        Console.Error.WriteLine($"Not a directory alias: {alias}");
                    }
                    if (normalArgs.Length == 1)
                    {
                        toReplace.First().Directory = Directory.GetCurrentDirectory();
                        File.WriteAllLines(fullPathToAliasFile, aliasList.Select(x => x.ToString()));
                    }
                    else
                    {
                        var dir = normalArgs[1];
                        if (!Directory.Exists(dir))
                        {
                            throw new Exception($"Directory does not exist: {dir}");
                        }
                        toReplace.First().Directory = dir;
                        File.WriteAllLines(fullPathToAliasFile, aliasList.Select(x => x.ToString()));
                    }
                }
                else if (deleteOne)
                {
                    if (normalArgs.Length == 0)
                    {
                        throw new Exception("You must supply an alias to delete.");
                    }
                    var alias = normalArgs[0];
                    var lk = aliasList.ToLookup(x => x.Alias);
                    var toDelete = lk[alias];
                    if (toDelete.Count() == 0)
                    {
                        Console.Error.WriteLine($"Not a directory alias: {alias}");
                    }
                    aliasList.Remove(toDelete.First());
                }
                else if (add)
                {
                    if (normalArgs.Length == 0)
                    {
                        throw new Exception("You must supply an alias to add.");
                    }
                    var directory = normalArgs.Length == 2 ? normalArgs[1] : Directory.GetCurrentDirectory();
                    var alias = new DirectoryAlias
                    {
                        Alias = normalArgs[0],
                        Directory = directory
                    };
                    aliasList.Remove(alias);
                }
                else if (printThis)
                {
                    var directory = normalArgs.Length == 0 ? Directory.GetCurrentDirectory() : normalArgs[0];
                    var lk = aliasList.ToLookup(x => x.Directory, StringComparer.OrdinalIgnoreCase);
                    if (lk.Count() > 0)
                    {
                        Console.WriteLine($"{lk[0].First().Directory}");
                    }
                }
            }
            catch (Exception ex)
            {
                var fullname = System.Reflection.Assembly.GetEntryAssembly().Location;
                var progname = Path.GetFileNameWithoutExtension(fullname);
                Console.Error.WriteLine($"{progname} Error: {ex.Message}");
            }

        }

        private static void PrintUsageAndExit()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("    ada [options] [<directory>]:");
            Console.WriteLine("Options:");
            Console.WriteLine("    -a <directory> - add directory alias. Uses current dir. if none supplied.");
            Console.WriteLine("    -d <alias> - remove alias. Uses alias for current directory if none supplied.");
            Console.WriteLine("    -r - replace directory alias.");
            Console.WriteLine("    -l - list all directory aliases.");
            Console.WriteLine("    -t - list aliases for this directory.");
            Console.WriteLine("    -u - list aliases for this directory and below.");
            Console.WriteLine("    -x - remove aliases for missing directories.");
            Environment.Exit(-1);
        }
    }
}
