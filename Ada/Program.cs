using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Ada
{
    internal class Program
    {
        private static string GetExactPathName(string pathName)
        {
            if (!(File.Exists(pathName) || Directory.Exists(pathName)))
                return pathName;

            var di = new DirectoryInfo(pathName);

            if (di.Parent != null)
            {
                return Path.Combine(
                    GetExactPathName(di.Parent.FullName),
                    di.Parent.GetFileSystemInfos(di.Name)[0].Name);
            }
            return di.FullName.ToUpper();
        }


        private static string Bashify(string dir)
        {
            var sb = new StringBuilder();
            if (dir.Length > 1 && dir[1] == ':' && char.IsLetter(dir[0]))
            {
                sb.Append('/');
                sb.Append(char.ToLower(dir[0]));
                sb.Append(dir.Substring(2).Replace('\\', '/'));
            }
            else
            {
                sb.Append(dir.Replace('\\', '/'));
            }
            return sb.ToString();
        }
        private static void Main(string[] args)
        {
            try
            {
                var dirRegex = new Regex(@"alias\s+(?'name'\w+)\s*=\s*'cd\s+(?'value'[^\s].+[^\s])'", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                if (args.Length == 0)
                {
                    PrintUsageAndExit();
                }
                var normalArgs = args.Where(x => x[0] != '-').ToArray();
                var optionArgs = args.Where(x => x[0] == '-').SelectMany(x => x.Skip(1)).ToHashSet();

                var folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var fullPathToAliasFile = Path.Combine(folder, ".dir-aliases");
                if (!File.Exists(fullPathToAliasFile))
                {
                    File.WriteAllText(fullPathToAliasFile, "");
                }

                var aliasList = new List<DirectoryAlias>();
                int lineNumber = 1;
                foreach (var line in File.ReadLines(fullPathToAliasFile).Select(x => x.Trim()))
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
                var listAll = optionArgs.Contains('l');
                var listAllAliases = optionArgs.Contains('m');

                var r = new[] { replace, deleteOne, add, printThis, listAll, listAllAliases }.ToLookup(x => x);
                if (r[true].Count() > 1)
                {
                    throw new Exception("Only one of -r, -a, -d -t, -l, -m can be supplied.");
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
                    else if (normalArgs.Length == 1)
                    {
                        toReplace.First().Directory = GetExactPathName(Bashify(Directory.GetCurrentDirectory()));
                        File.WriteAllLines(fullPathToAliasFile, aliasList.Select(x => x.ToString()));
                    }
                    else
                    {
                        var dir = normalArgs[1];
                        if (!Directory.Exists(dir))
                        {
                            throw new Exception($"Directory does not exist: {dir}");
                        }
                        toReplace.First().Directory = GetExactPathName(Bashify(dir));
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
                        throw new Exception($"Not a directory alias: {alias}");
                    }
                    aliasList.Remove(toDelete.First());
                    File.WriteAllLines(fullPathToAliasFile, aliasList.Select(x => x.ToString()));
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
                        Directory = GetExactPathName(Bashify(directory))
                    };
                    aliasList.Add(alias);
                    File.WriteAllLines(fullPathToAliasFile, aliasList.Select(x => x.ToString()));
                }
                else if (printThis)
                {
                    var directory = normalArgs.Length == 0 ? Directory.GetCurrentDirectory() : normalArgs[0];
                    var lk = aliasList.ToLookup(x => x.Directory, StringComparer.OrdinalIgnoreCase);
                    if (lk[directory].Count() > 0)
                    {
                        Console.WriteLine($"{lk[directory].First().Alias}");
                    }
                }
                else if (listAll)
                {
                    foreach (var alias in aliasList)
                    {
                        Console.WriteLine(alias);
                    }
                }
                else if (listAllAliases)
                {
                    var print = string.Join(", ", aliasList.Select(x => x.Alias));
                    Console.WriteLine(print);
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
