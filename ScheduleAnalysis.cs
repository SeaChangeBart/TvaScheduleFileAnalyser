using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MeasureScheduleDuration
{
    internal class ScheduleAnalysis
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

        public class WrongEventInfo
        {
            public ScheduleEvent ScheduleEvent { get; set; }
            public int NumberOfReruns { get; set; }
            public TimeSpan Age { get; set; }

            public static WrongEventInfo FromTva(ScheduleEvent se, TvaMain tva, DateTime fileDate)
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

        public static ScheduleAnalysis FromTva(ScheduleAnalysis prevOrNull, FileInfo fi)
        {
            var fileDate = fi.LastWriteTimeUtc;
            try
            {
                var tva = XDocument.Load(fi.FullName).ParseAsTva();
                var refDate = fileDate.Subtract(TimeSpan.FromMinutes(5)); // Catch false positives (race cond.)

                var singleSchedule = tva.ProgramDescription.ProgramLocationTable.Schedules.Single();

                var serviceInformationTable = tva.ProgramDescription.ServiceInformationTable;
                return new ScheduleAnalysis
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
                            .Select(se => WrongEventInfo.FromTva(se, tva, fileDate))
                            .ToArray(),
                };
            }
            catch (Exception e)
            {
                return new ScheduleAnalysis
                {
                    FileName = fi.Name,
                    Date = fileDate,
                    ServiceName = "Error: " + e.Message,
                    WrongEvents = new WrongEventInfo[0]
                };
            }
        }
    }
}