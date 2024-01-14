using System.Collections.Generic;
using System.Xml.Serialization;

namespace MxlToUstConverter.Models.MusicXML;

public class Part
{
    [XmlAttribute("id")]
    public string? Id { get; set; }

    [XmlElement("measure")]
    public List<Measure>? Measures { get; set; }

    public override string ToString()
        => $"Id={this.Id}, Measures={this.Measures}";
}
