using System.Xml.Serialization;

namespace MxlToUstConverter.Models.MusicXML.DirectionElements;

public class DirectionType
{
    [XmlElement("metronome")]
    public MetronomeType? Metronome { get; set; }
}
