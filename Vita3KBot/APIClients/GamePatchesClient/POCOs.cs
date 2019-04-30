using System;
using System.Xml;
using System.Xml.Serialization;

namespace GamePatchesClient.POCOs
{
    //Game updates POCOs
    [Serializable, XmlType("titlepatch")]
    public class TitlePatch
    {
        [XmlElement(typeof(Tag), ElementName = "tag")]
        public Tag Tag { get; set; }
    }

    public class Tag
    {
        [XmlElement(typeof(Package), ElementName = "package")]
        public Package[] Package { get; set; }
    }

    public class Package
    {
        [XmlAttribute(AttributeName = "version")]
        public string Version { get; set; }

        [XmlAttribute(AttributeName = "size")]
        public double Size { get; set; }

        [XmlAttribute(AttributeName = "url")]
        public string Url { get; set; }

        [XmlAttribute(AttributeName = "psp2_system_ver")]
        public uint SysVer { get; set; }

        [XmlAttribute(AttributeName = "content_id")]
        public string ContentId { get; set; }

        [XmlElement(ElementName = "paramsfo")]
        public ParamSfo Sfo { get; set; }

        [XmlElement(typeof(Package), ElementName = "hybrid_package")]
        public Package HybridPackage { get; set; }
    }

    public class ParamSfo
    {
        [XmlElement(ElementName = "title")]
        public string Title { get; set; }
    }


    //Covers POCOs
    public class Root
    {
        public IDs[] IDs { get; set; }
    }

    public class IDs
    {
        public string ID { get; set; }
        public string cover { get; set; }
    }
}
