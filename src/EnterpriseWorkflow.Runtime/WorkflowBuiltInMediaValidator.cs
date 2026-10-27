using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Bounded format recognition. It does not execute content, render documents or replace malware inspection.</summary>
public static class WorkflowBuiltInMediaValidator
{
    public static string Detect(byte[] bytes, string extension, CancellationToken token = default)
    {
        var format = WorkflowMediaFormats.BuiltIns.SingleOrDefault(item => item.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("Unsupported file format.");
        token.ThrowIfCancellationRequested();
        var valid = extension switch
        {
            ".pdf" => bytes.AsSpan().StartsWith("%PDF-"u8),
            ".png" => bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" or ".jpeg" => bytes.Length > 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255,
            ".txt" or ".csv" => IsText(bytes),
            ".rtf" => IsRtf(bytes, token),
            ".tif" or ".tiff" => IsTiff(bytes),
            ".docx" or ".xlsx" or ".pptx" or ".odt" or ".ods" or ".odp" => IsOfficePackage(bytes, extension, format.ContentType, token),
            _ => false
        };
        return valid ? format.ContentType : throw new InvalidDataException("File content does not match its format.");
    }
    private static bool IsText(byte[] bytes)
    {
        try { _ = new UTF8Encoding(false, true).GetString(bytes); return !bytes.AsSpan().Contains((byte)0); }
        catch (DecoderFallbackException) { return false; }
    }
    private static bool IsRtf(byte[] bytes, CancellationToken token)
    {
        if (!bytes.AsSpan().StartsWith("{\\rtf1"u8)) return false;
        var depth = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            var current = bytes[i];
            if (current == '{') { if (++depth > 256) return false; }
            else if (current == '}') { if (--depth < 0) return false; if (depth == 0) return bytes.AsSpan(i + 1).Trim(" \r\n\t"u8).IsEmpty; }
            else if (current == '\\')
            {
                if (++i >= bytes.Length) return false;
                if (bytes[i] is (byte)'\\' or (byte)'{' or (byte)'}') continue;
                if (bytes[i] == '\'') { if (i + 2 >= bytes.Length || !Uri.IsHexDigit((char)bytes[i + 1]) || !Uri.IsHexDigit((char)bytes[i + 2])) return false; i += 2; continue; }
                var start = i;
                while (i < bytes.Length && char.IsAsciiLetter((char)bytes[i])) i++;
                var word = Encoding.ASCII.GetString(bytes, start, i - start);
                if (word is "object" or "objdata") return false;
                var negative = i < bytes.Length && bytes[i] == '-'; if (negative) i++;
                long number = 0; var digits = 0;
                while (i < bytes.Length && char.IsAsciiDigit((char)bytes[i])) { if (++digits > 10) return false; number = number * 10 + bytes[i++] - '0'; }
                if (i < bytes.Length && bytes[i] == ' ') i++;
                if (word == "bin") { if (negative || digits == 0 || number > bytes.Length - i) return false; i += (int)number; }
                i--;
            }
        }
        return false;
    }
    private static bool IsTiff(byte[] bytes)
    {
        if (bytes.Length < 8) return false;
        var little = bytes[0] == 'I' && bytes[1] == 'I';
        if (!little && !(bytes[0] == 'M' && bytes[1] == 'M')) return false;
        ushort U16(int offset) => little ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2)) : BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
        uint U32(int offset) => little ? BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)) : BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
        if (U16(2) != 42) return false; // Baseline TIFF; BigTIFF is a separate format.
        var next = U32(4); var visited = new HashSet<uint>();
        while (next != 0)
        {
            if (visited.Count >= 64 || !visited.Add(next) || next < 8 || next > bytes.Length - 2) return false;
            var offset = (int)next; var count = U16(offset);
            if (count is 0 or > 512 || (long)offset + 2 + 12L * count + 4 > bytes.Length) return false;
            uint[]? offsets = null, lengths = null; uint width = 0, height = 0;
            for (var j = 0; j < count; j++)
            {
                var entry = offset + 2 + j * 12; var tag = U16(entry); var type = U16(entry + 2); var items = U32(entry + 4);
                if (tag is not (256 or 257 or 273 or 279 or 324 or 325)) continue;
                if (type is not (3 or 4) || items is 0 or > 4096) return false;
                var size = type == 3 ? 2 : 4; var position = (long)items * size <= 4 ? (uint)(entry + 8) : U32(entry + 8);
                if (position + (long)items * size > bytes.Length) return false;
                var values = Enumerable.Range(0, (int)items).Select(index => size == 2 ? (uint)U16((int)position + index * size) : U32((int)position + index * size)).ToArray();
                if (tag == 256) width = values[0]; else if (tag == 257) height = values[0];
                else if (tag is 273 or 324) offsets = values; else lengths = values;
            }
            if (width == 0 || height == 0 || offsets is null || lengths is null || offsets.Length != lengths.Length ||
                offsets.Where((value, index) => lengths[index] == 0 || (long)value + lengths[index] > bytes.Length).Any()) return false;
            next = U32(offset + 2 + count * 12);
        }
        return visited.Count > 0;
    }
    private static bool IsOfficePackage(byte[] bytes, string extension, string mime, CancellationToken token)
    {
        try
        {
            using var input = new MemoryStream(bytes, false);
            using var zip = new ZipArchive(input, ZipArchiveMode.Read);
            if (zip.Entries.Count is 0 or > 2048) return false;
            long expanded = 0; var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var documents = new Dictionary<string, XDocument>(StringComparer.Ordinal);
            string? packageMime = null;
            foreach (var entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested();
                var name = entry.FullName;
                if (name.Length > 512 || name.StartsWith('/') || name.Contains('\\') || name.Split('/').Any(part => part is "." or "..") || !names.Add(name) ||
                    name.StartsWith("Basic/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Scripts/", StringComparison.OrdinalIgnoreCase) || name.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) || name.Contains("/activeX/", StringComparison.OrdinalIgnoreCase) || name.Contains("/embeddings/", StringComparison.OrdinalIgnoreCase)) return false;
                expanded += entry.Length;
                if (expanded > 200L * 1024 * 1024 || entry.Length > 100L * 1024 * 1024 || (entry.Length > 1024 * 1024 && entry.Length > Math.Max(1, entry.CompressedLength) * 100)) return false;
                if (name.EndsWith('/')) continue;
                var xml = name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase);
                if (xml && entry.Length > 32L * 1024 * 1024) return false;
                using var source = entry.Open();
                using var captured = xml || name == "mimetype" ? new MemoryStream() : null;
                var buffer = new byte[65536]; long actual = 0; int read;
                while ((read = source.Read(buffer)) > 0) { token.ThrowIfCancellationRequested(); actual += read; if (actual > entry.Length) return false; captured?.Write(buffer, 0, read); }
                if (actual != entry.Length) return false;
                if (name == "mimetype") { if (entry.Length > 200) return false; packageMime = Encoding.ASCII.GetString(captured!.ToArray()); }
                if (!xml) continue;
                captured!.Position = 0;
                using var reader = XmlReader.Create(captured, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32L * 1024 * 1024, MaxCharactersFromEntities = 0 });
                var selected = name is "[Content_Types].xml" or "_rels/.rels" or "word/document.xml" or "xl/workbook.xml" or "ppt/presentation.xml" or "content.xml" or "META-INF/manifest.xml";
                if (selected)
                {
                    while (reader.Read()) { token.ThrowIfCancellationRequested(); if (reader.Depth > 64) return false; }
                    captured.Position = 0;
                    using var documentReader = XmlReader.Create(captured, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32L * 1024 * 1024 });
                    documents[name] = XDocument.Load(documentReader);
                }
                else while (reader.Read()) { token.ThrowIfCancellationRequested(); if (reader.Depth > 64) return false; }
            }
            if (extension is ".odt" or ".ods" or ".odp")
            {
                const string ns = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
                if (packageMime != mime || !documents.TryGetValue("META-INF/manifest.xml", out var manifest) ||
                    manifest.Root?.Name != XName.Get("manifest", ns) || manifest.Descendants().Any(node => node.Name.LocalName == "encryption-data") ||
                    !manifest.Descendants(XName.Get("file-entry", ns)).Any(node => (string?)node.Attribute(XName.Get("full-path", ns)) == "/" && (string?)node.Attribute(XName.Get("media-type", ns)) == mime) ||
                    !documents.TryGetValue("content.xml", out var content) || content.Root?.Name != XName.Get("document-content", "urn:oasis:names:tc:opendocument:xmlns:office:1.0")) return false;
                if (content.Descendants().Any(node => node.Name.NamespaceName == "urn:oasis:names:tc:opendocument:xmlns:script:1.0" || (node.Name.LocalName == "scripts" && node.HasElements))) return false;
                var body = content.Root.Element(XName.Get("body", "urn:oasis:names:tc:opendocument:xmlns:office:1.0"));
                var kind = extension == ".odt" ? "text" : extension == ".ods" ? "spreadsheet" : "presentation";
                return body?.Element(XName.Get(kind, "urn:oasis:names:tc:opendocument:xmlns:office:1.0")) is not null;
            }
            var path = extension == ".docx" ? "word/document.xml" : extension == ".xlsx" ? "xl/workbook.xml" : "ppt/presentation.xml";
            var mainType = mime + ".main+xml";
            const string types = "http://schemas.openxmlformats.org/package/2006/content-types";
            if (!documents.TryGetValue("[Content_Types].xml", out var contentTypes) || contentTypes.Root?.Name != XName.Get("Types", types) ||
                contentTypes.Descendants().Any(node => ((string?)node.Attribute("ContentType"))?.Contains("macro", StringComparison.OrdinalIgnoreCase) == true) ||
                !contentTypes.Descendants(XName.Get("Override", types)).Any(node => (string?)node.Attribute("PartName") == "/" + path && (string?)node.Attribute("ContentType") == mainType) ||
                !documents.TryGetValue(path, out var main) || !documents.TryGetValue("_rels/.rels", out var relationships)) return false;
            var family = extension == ".docx" ? "wordprocessingml" : extension == ".xlsx" ? "spreadsheetml" : "presentationml";
            var root = extension == ".docx" ? "document" : extension == ".xlsx" ? "workbook" : "presentation";
            if (main.Root?.Name.LocalName != root || main.Root.Name.NamespaceName is not { } documentNamespace ||
                (documentNamespace != $"http://schemas.openxmlformats.org/{family}/2006/main" && documentNamespace != $"http://purl.oclc.org/ooxml/{family}/main")) return false;
            return relationships.Descendants().Any(node => node.Name.LocalName == "Relationship" &&
                ((string?)node.Attribute("Type")) is "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" or "http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument" &&
                (string?)node.Attribute("TargetMode") != "External" && ((string?)node.Attribute("Target"))?.TrimStart('/') == path);
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or IOException or ArgumentException) { return false; }
    }
}
