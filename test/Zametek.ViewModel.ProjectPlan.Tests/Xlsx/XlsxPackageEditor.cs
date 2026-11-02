using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Changes a workbook as a person does who opens it in Excel and edits it: removes a sheet, types text into a cell. It
    /// changes the XML of the package and nothing else, so it works on a workbook whatever library wrote it.
    /// </summary>
    internal sealed class XlsxPackageEditor
    {
        private static readonly XNamespace s_Main = @"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace s_Relationships = @"http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace s_Package = @"http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace s_ContentTypes = @"http://schemas.openxmlformats.org/package/2006/content-types";

        private readonly Dictionary<string, XDocument> m_Parts = [];
        private readonly Dictionary<string, byte[]> m_Raw = [];
        private readonly List<string> m_Order = [];

        private XlsxPackageEditor(byte[] package)
        {
            using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                using Stream stream = entry.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                m_Raw[entry.FullName] = buffer.ToArray();
                m_Order.Add(entry.FullName);
            }
        }

        public static byte[] Edit(byte[] package, Action<XlsxPackageEditor> change)
        {
            ArgumentNullException.ThrowIfNull(package);
            ArgumentNullException.ThrowIfNull(change);
            var editor = new XlsxPackageEditor(package);
            change(editor);
            return editor.Save();
        }

        private XDocument Part(string name)
        {
            if (!m_Parts.TryGetValue(name, out XDocument? document))
            {
                using var stream = new MemoryStream(m_Raw[name]);
                document = XDocument.Load(stream);
                m_Parts[name] = document;
            }

            return document;
        }

        private string SheetPart(string sheetName, out XElement sheetElement)
        {
            XDocument workbook = Part(@"xl/workbook.xml");
            sheetElement = workbook.Descendants(s_Main + @"sheet")
                .Single(x => string.Equals((string?)x.Attribute(@"name"), sheetName, StringComparison.OrdinalIgnoreCase));
            string id = (string)sheetElement.Attribute(s_Relationships + @"id")!;
            string target = Part(@"xl/_rels/workbook.xml.rels").Descendants(s_Package + @"Relationship")
                .Single(x => (string?)x.Attribute(@"Id") == id)
                .Attribute(@"Target")!.Value;
            return target.StartsWith('/') ? target[1..] : $@"xl/{target}";
        }

        public XlsxPackageEditor RemoveSheet(string sheetName)
        {
            string part = SheetPart(sheetName, out XElement sheet);
            string id = (string)sheet.Attribute(s_Relationships + @"id")!;
            sheet.Remove();
            Part(@"xl/_rels/workbook.xml.rels").Descendants(s_Package + @"Relationship").Single(x => (string?)x.Attribute(@"Id") == id).Remove();
            Part(@"[Content_Types].xml").Descendants(s_ContentTypes + @"Override").Where(x => (string?)x.Attribute(@"PartName") == $@"/{part}").ToList().ForEach(x => x.Remove());
            m_Raw.Remove(part);
            m_Parts.Remove(part);
            m_Order.Remove(part);
            return this;
        }

        // What a person does who types text into a cell: the cell holds that text and nothing else.
        public XlsxPackageEditor SetText(string sheetName, string reference, string text)
        {
            XDocument worksheet = Part(SheetPart(sheetName, out _));
            XElement cell = worksheet.Descendants(s_Main + @"c").Single(x => (string?)x.Attribute(@"r") == reference);
            cell.RemoveNodes();
            cell.SetAttributeValue(@"t", @"inlineStr");
            cell.Add(new XElement(s_Main + @"is", new XElement(s_Main + @"t", new XAttribute(XNamespace.Xml + @"space", @"preserve"), text)));
            return this;
        }

        private byte[] Save()
        {
            using var package = new MemoryStream();
            using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (string name in m_Order)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(name);
                    using Stream stream = entry.Open();

                    if (m_Parts.TryGetValue(name, out XDocument? document))
                    {
                        document.Save(stream);
                    }
                    else
                    {
                        stream.Write(m_Raw[name]);
                    }
                }
            }

            return package.ToArray();
        }
    }
}
