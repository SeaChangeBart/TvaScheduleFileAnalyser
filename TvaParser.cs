using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace TvaScheduleFileAnalyser
{
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
}
