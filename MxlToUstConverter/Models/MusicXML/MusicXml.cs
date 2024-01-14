﻿using System.Collections.Generic;
using System.Xml.Serialization;
using MxlToUstConverter.Models.MusicXML.Metadata;

namespace MxlToUstConverter.Models.MusicXML;

[XmlRoot("score-partwise")]
public class MusicXmlObject
{
    [XmlAttribute("version")]
    public string? Version { get; set; }

    [XmlElement("identification")]
    public Identification? Identification { get; set; }

    [XmlElement("part-list")]
    public PartList? PartList { get; set; }

    [XmlElement("part")]
    public List<Part>? Parts { get; set; }

    public override string ToString()
        => $"Version={this.Version}, Identification={this.Identification}, PartList={this.PartList}, Parts={this.Parts}";
}
