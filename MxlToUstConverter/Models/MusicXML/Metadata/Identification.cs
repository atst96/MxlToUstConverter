using System.Xml.Serialization;

namespace MxlToUstConverter.Models.MusicXML.Metadata;

public class Identification
{
    [XmlElement("encoding")]
    public ScoreEncoding? Encoding { get; set; }

    public override string ToString()
        => $"Encoding={this.Encoding}";
}
