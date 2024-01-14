using System.Xml.Serialization;

namespace MxlToUstConverter.Models.MusicXML.MeasureAttributeElements;

public class Key
{
    [XmlElement("fifths")]
    public int Fifths { get; set; }

    public override string ToString()
        => $"Fifths={this.Fifths}";
}
