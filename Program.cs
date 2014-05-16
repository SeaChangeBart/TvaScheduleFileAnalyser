using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TvaScheduleFileAnalyser
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Log("Usage: TvaScheduleFileAnalyser.exe [dir] [daysHistory]");

            var path = args.FirstOrDefault() ?? ".";
            var daysHistory = ParseIntOr(args.Skip(1).FirstOrDefault(), 60);
            var cutOffDate = DateTime.UtcNow.AddDays(-daysHistory);

            Log("Scanning {0} for TVA files, max {1} days old", path, daysHistory);

            var dirInfo = new DirectoryInfo(path);

            var files = CollectTvaFiles(dirInfo, cutOffDate).ToArray();
            Log("{0} (possible) TVA files", files.Length);

            var results = files.Analyse();

            var tsvLines = results.AsTsv();

            // Iterate lazily, draw a dot even for 'null' lines (OK files) to show progress always.
            foreach (var l in tsvLines)
            {
                if (l != null)
                    Output(l);
                Log('.');
            }
        }

        private static void Log(string s, params object[] p)
        {
            Console.Error.WriteLine(s, p);
        }

        private static void Log(char s)
        {
            Console.Error.Write(s);
        }

        private static void Output(string s, params object[] p)
        {
            Console.WriteLine(s, p);
        }

        private static IEnumerable<string> AsTsv(this IEnumerable<ScheduleAnalysis> results)
        {
            return results
                .Select(ToTsvString_IfErrorOrWrong)
                .StartWith(GetTsvHeader());
        }

        private static IEnumerable<ScheduleAnalysis> Analyse(this IEnumerable<FileInfo> files)
        {
            return files.Scan((ScheduleAnalysis) null, ScheduleAnalysis.FromTva);
        }

        private static IEnumerable<FileInfo> CollectTvaFiles(DirectoryInfo dirInfo, DateTime cutOffDate)
        {
            return dirInfo.EnumerateFiles("*.xml", SearchOption.AllDirectories)
                .Where(IsNoResponseFile) // Parsing Response file as TVA would lead to thousands of errors in the log
                .Where(fi => fi.LastWriteTimeUtc >= cutOffDate)
                .OrderBy(fi => fi.LastWriteTimeUtc);
        }

        private static int ParseIntOr(string src, int fallback)
        {
            int retVal;
            return int.TryParse(src, out retVal) ? retVal : fallback;
        }

        private static bool IsNoResponseFile( FileInfo fi)
        {
            return !fi.Name.Contains("_Loaded");
        }

        private static string ToTsvString_IfErrorOrWrong(ScheduleAnalysis analysis, int index)
        {
            if (analysis.ServiceId == null)
                return ToTsvString_Error(analysis, index);
            return analysis.WrongEvents.Any() ? ToTsvString(analysis, index) : null;
        }

        private static string ToTsvString(ScheduleAnalysis r, int index)
        {
            return string.Format("{9}\t{0}\t{1:yyyy-MM-dd HH:mm}\t{2:###} hr\t{3}\t{4}\t{5:g}\t{6:0.0}\t{7}\t{8}",
                r.FileName,
                r.Date,
                r.TimeSpan.TotalHours,
                r.WrongEvents.Count(),
                r.WrongEvents.Count(we => we.NumberOfReruns > 0),
                r.WrongEvents.Any() ? r.WrongEvents.Min(we => we.Age) : default(TimeSpan),
                (r.Date - r.PreviousFileDate).TotalMinutes,
                r.ServiceId,
                r.ServiceName,
                index);
        }

        private static string GetTsvHeader()
        {
            return string.Format("{9}\t{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}",
                "File",
                "Date",
                "ScheduleHrs",
                "Wrong Events",
                "Of which have reruns",
                "Age of wrong event",
                "Mins since last file",
                "Service Id",
                "Service Name",
                "File #");
        }

        private static string ToTsvString_Error(ScheduleAnalysis r, int index)
        {
            return string.Format("{3}\t{0}\t{1:yyyy-MM-dd HH:mm}\t\t\t\t\t\t\t{2}",
                r.FileName,
                r.Date,
                r.ServiceName,
                index);

        }
    }
}
