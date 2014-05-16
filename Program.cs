using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MeasureScheduleDuration
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var path = args.FirstOrDefault() ?? ".";
            var dirInfo = new DirectoryInfo(path);
            Console.Error.WriteLine("Scanning {0}", path);
            var files =
                dirInfo.EnumerateFiles("*.xml", SearchOption.AllDirectories)
                    .Where(fi => !fi.Name.Contains("_Loaded"))
                    .Where(fi => fi.LastWriteTimeUtc.Year == 2014)
                    .OrderBy(fi => fi.LastWriteTimeUtc).ToArray();

            Console.Error.WriteLine("{0} (possible) TVA files", files.Length);

            var results = files.Scan((ScheduleInfo) null, GetScheduleInfo);

            var lines = results.Select(PrintResult).StartWith(PrintHeader());

            foreach (var l in lines)
            {
                if (l!=null)
                    Console.WriteLine(l);
                    Console.Error.Write(".");
            }
        }

        private static string PrintResult(ScheduleInfo info, int index)
        {
            if (info.ServiceId == null)
                return PrintException(info, index);
            else if (info.WrongEvents.Any())
                return PrintWrong(info, index);
            else
                return null;
        }

        private static string PrintWrong(ScheduleInfo r, int index)
        {
            return string.Format("{9}\t{0}\t{1:yyyy-MM-dd HH:mm}\t{2:###} hr\t{3}\t{4}\t{5:g}\t{6:0.0}\t{7}\t{8}",
                r.FileName,
                r.Date,
                r.TimeSpan.TotalHours,
                r.WrongEvents.Count(),
                r.WrongEvents.Count(we => we.NumberOfReruns > 0),
                r.WrongEvents.Any()?r.WrongEvents.Min(we => we.Age):default(TimeSpan),
                (r.Date - r.PreviousFileDate).TotalMinutes,
                r.ServiceId,
                r.ServiceName,
                index);
        }

        private static string PrintHeader()
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

        private static string PrintException(ScheduleInfo r, int index)
        {
            return string.Format("{3}\t{0}\t{1:yyyy-MM-dd HH:mm}\t\t\t\t\t\t\t{2}",
                r.FileName,
                r.Date,
                r.ServiceName,
                index);

        }

        private static readonly TimeSpan BatchThreshold = TimeSpan.FromMinutes(1);
        private static ScheduleInfo GetScheduleInfo(ScheduleInfo prevOrNull, FileInfo fi)
        {
            var fileDate = fi.LastWriteTimeUtc;
            try
            {
                var tva = XDocument.Load(fi.FullName).ParseAsTva();
                var refDate = fileDate.Subtract(TimeSpan.FromMinutes(1)); // Catch false positives (race cond.)

                var singleSchedule = tva.ProgramDescription.ProgramLocationTable.Schedules.Single();

                var serviceInformationTable = tva.ProgramDescription.ServiceInformationTable;
                return new ScheduleInfo
                {
                    PreviousFileDate = prevOrNull == null ? fileDate.AddHours(-6) : prevOrNull.Date,
                    FileName = fi.Name,
                    Date = fileDate,
                    ServiceId = singleSchedule.ServiceId,
                    ServiceName =
                        serviceInformationTable.ServiceInformation(singleSchedule.ServiceId)
                            .Names.Longest(),
                    StartTime = singleSchedule.StartTime,
                    EndTime = singleSchedule.EndTime,
                    WrongEvents =
                        singleSchedule.ScheduleEvents.Where(se => se.EndTime < refDate)
                            .Select(se => WrongEventInfo(se, tva, fileDate))
                            .ToArray(),
                };
            }
            catch (Exception e)
            {
                return new ScheduleInfo
                {
                    FileName = fi.Name,
                    Date = fileDate,
                    ServiceName = "Error: " + e.Message,
                    WrongEvents = new WrongEventInfo[0]
                };
            }
        }

        private static WrongEventInfo WrongEventInfo(ScheduleEvent se, TvaMain tva, DateTime fileDate)
        {
            var singleSchedule = tva.ProgramDescription.ProgramLocationTable.Schedules.Single();
            return new WrongEventInfo
            {
                ScheduleEvent = se,
                NumberOfReruns = singleSchedule.ScheduleEvents.Count(cand => cand.ProgramId.Equals(se.ProgramId)) - 1,
                Age = fileDate - se.EndTime,
            };
        }
    }

    internal class WrongEventInfo
    {
        public ScheduleEvent ScheduleEvent { get; set; }
        public int NumberOfReruns { get; set; }
        public TimeSpan Age { get; set; }
    }

    public static class Extensions
    {
        public static DateTime Floor(this DateTime dt, TimeSpan segment)
        {
            var date = dt.Date;
            var time = dt.TimeOfDay;
            var parts = (int)(time.Ticks / segment.Ticks);
            return date + TimeSpan.FromTicks(segment.Ticks * parts);
        }

        public static string Longest(this IEnumerable<string> src)
        {
            return src.OrderByDescending(v => v.Length)
                .First();
        }

        public static IEnumerable<TResult> SelectOrSkip<TSource, TResult>(this IEnumerable<TSource> src,
            Func<TSource, TResult> fn) where TResult : class
        {
            return src.SelectOrNull(fn).Where(i => i != null);
        }

        public static IEnumerable<TResult> SelectNonNull<TSource, TResult>(this IEnumerable<TSource> src,
            Func<TSource, TResult> fn) where TResult : class
        {
            return src.Select(fn).Where(i => i != null);
        }

        public static IEnumerable<TResult> SelectNonNull<TSource, TResult>(this IEnumerable<TSource> src,
            Func<TSource, int, TResult> fn) where TResult : class
        {
            return src.Select(fn).Where(i => i != null);
        }

        public static
            IEnumerable<TResult> SelectOrNull<TSource, TResult>(this IEnumerable<TSource> src,
                Func<TSource, TResult> fn) where TResult:class
        {
            return
                src.Select(i =>
                {
                    try
                    {
                        return
                            fn(i);
                    }
                    catch
                    {
                        return null;
                    }
                }
                    );

        }
    }

    public static class TvaParser
    {
        public static TvaMain ParseAsTva(this XDocument doc)
        {
            var tvaMain = doc.SingleTvaElement("TVAMain");
            return new TvaMain
            {
                ProgramDescription = ParseProgramDescription(tvaMain)
            };
        }

        private static IEnumerable<XElement> TvaElements(this XElement parent, string localName)
        {
            return parent.Elements(Tva2010Ns + localName);
        }

        private static XElement SingleTvaElement(this XElement parent, string localName)
        {
            return parent.SingleElement(Tva2010Ns + localName);
        }

        private static XElement SingleElement(this XElement parent, XName name)
        {
            try
            {
                return parent.Elements(name).Single();
            }
            catch (Exception)
            {
                throw new InvalidOperationException(
                    string.Format("Expected exactly one element {0} below {1}; {2} found.", name.LocalName,
                        parent.Name.LocalName, parent.Elements(name).Count()));
            }
        }

        private static XAttribute SingleAttribute(this XElement parent, XName name)
        {
            try
            {
                return parent.Attributes(name).Single();
            }
            catch (Exception)
            {
                throw new InvalidOperationException(
                    string.Format("Expected exactly one attribute {0} in {1}; {2} found.", name.LocalName,
                        parent.Name.LocalName, parent.Attributes(name).Count()));
            }
        }

        private static XElement SingleTvaElement(this XDocument parent, string localName)
        {
            return parent.SingleElement(Tva2010Ns + localName);
        }

        private static XElement SingleElement(this XDocument parent, XName name)
        {
            try
            {
                return parent.Elements(name).Single();
            }
            catch (Exception)
            {
                throw new InvalidOperationException(
                    string.Format("Expected exactly one element {0} in document; {1} found.", name.LocalName,
                        parent.Elements(name).Count()));
            }
        }

        private static readonly XNamespace Tva2010Ns = "urn:tva:metadata:2010";

        private static ProgramDescription ParseProgramDescription(XElement tvaMainElem)
        {
            var pdElement = tvaMainElem.SingleTvaElement("ProgramDescription");
            return new ProgramDescription
            {
                ProgramLocationTable = ParseProgramLocationTable(pdElement),
                ServiceInformationTable = ParseServiceInformationTable(pdElement),
            };
        }

        private static ServiceInformationTable ParseServiceInformationTable(XElement pdElement)
        {
            var sitElement = pdElement.SingleTvaElement("ServiceInformationTable");
            return new ServiceInformationTable
            {
                ServiceInformations = ParseServiceInformations( sitElement).ToArray(),
            };
        }

        private static IEnumerable<ServiceInformation> ParseServiceInformations(XElement sitElement)
        {
            return sitElement.TvaElements("ServiceInformation").Select(siElem => new ServiceInformation
            {
                ServiceId = siElem.SingleAttribute("serviceId").Value,
                Names = siElem.TvaElements("Name").Select(ne => ne.Value).ToArray(),
            }
                );
        }

        private static ProgramLocationTable ParseProgramLocationTable(XElement pdElement)
        {
            var pltElement = pdElement.SingleTvaElement("ProgramLocationTable");
            return new ProgramLocationTable
            {
                Schedules = ParseSchedules(pltElement).ToArray()
            };
        }

        private static IEnumerable<Schedule> ParseSchedules(XElement pltElement)
        {
            return pltElement.TvaElements("Schedule").Select(sElem => new Schedule
            {
                ServiceId = sElem.SingleAttribute("serviceIDRef").Value,
                StartTime = sElem.SingleAttribute("start").AsDateTime(),
                EndTime = sElem.SingleAttribute("end").AsDateTime(),
                ScheduleEvents = ParseScheduleEvents(sElem).ToArray()
            });
        }

        private static IEnumerable<ScheduleEvent> ParseScheduleEvents(XElement sElem)
        {
            return sElem.TvaElements("ScheduleEvent").Select(seElem => new ScheduleEvent
            {
                ProgramId = seElem.SingleTvaElement("Program").SingleAttribute("crid").Value,
                StartTime = seElem.SingleTvaElement("PublishedStartTime").AsDateTime(),
                EndTime = seElem.SingleTvaElement("PublishedEndTime").AsDateTime(),
            });
        }

        private static DateTime AsDateTime(this string value)
        {
            return DateTime.Parse(value, null, DateTimeStyles.RoundtripKind);
        }

        private static DateTime AsDateTime(this XElement elem)
        {
            return elem.Value.AsDateTime();
        }

        private static DateTime AsDateTime(this XAttribute attr)
        {
            return attr.Value.AsDateTime();
        }
    }

    public class TvaMain
    {
        public ProgramDescription ProgramDescription { get; set; }
    }

    public class ProgramDescription
    {
        public ProgramLocationTable ProgramLocationTable { get; set; }
        public ServiceInformationTable ServiceInformationTable { get; set; }
    }

    public class ServiceInformationTable
    {
        public ServiceInformation[] ServiceInformations { get; set; }

        public ServiceInformation ServiceInformation(string id)
        {
            return ServiceInformations.Single(si => si.ServiceId.Equals(id));
        }
    }

    public class ServiceInformation
    {
        public string ServiceId { get; set; }
        public string[] Names { get; set; }
    }

    public class ProgramLocationTable
    {
        public Schedule[] Schedules { get; set; }
    }

    public class ScheduleEvent
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string ProgramId { get; set; }
    }

    public class Schedule
    {
        public string ServiceId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public ScheduleEvent[] ScheduleEvents { get; set; }
    }

    internal class ScheduleInfo
    {
        public string ServiceId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public TimeSpan TimeSpan
        {
            get { return EndTime - StartTime; }
        }

        public string ServiceName { get; set; }
        public string FileName { get; set; }
        public DateTime Date { get; set; }
        public WrongEventInfo[] WrongEvents { get; set; }
        public DateTime PreviousFileDate { get; set; }
        public bool FirstInBatch { get { return (Date - PreviousFileDate) < TimeSpan.FromMinutes(1); } }
    }
}