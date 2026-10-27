using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using EnterpriseWorkflow.Runtime;
using Xunit;

namespace EnterpriseWorkflow.Runtime.Tests;

public sealed class WorkflowOfficeFormatTests
{
    [Theory]
    [InlineData(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData(".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData(".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    [InlineData(".odt", "application/vnd.oasis.opendocument.text")]
    [InlineData(".ods", "application/vnd.oasis.opendocument.spreadsheet")]
    [InlineData(".odp", "application/vnd.oasis.opendocument.presentation")]
    public void OfficePackagesAreRecognizedByTheirPartsAndType(string extension, string mime)
    {
        var bytes = Package(extension, mime);
        Assert.Equal(mime, Detect(bytes, extension));
        Assert.Throws<InvalidDataException>(() => Detect(bytes, extension == ".xlsx" ? ".docx" : ".xlsx"));
    }
    [Theory]
    [InlineData("macro")]
    [InlineData("traversal")]
    [InlineData("dtd")]
    [InlineData("ratio")]
    [InlineData("depth")]
    [InlineData("odf-macro")]
    public void MalformedOrUnsupportedOfficePackagesFailClosed(string attack)
    {
        var extension = attack == "odf-macro" ? ".odt" : ".docx";
        var mime = attack == "odf-macro" ? "application/vnd.oasis.opendocument.text" : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        var bytes = Package(extension, mime, attack);
        Assert.Throws<InvalidDataException>(() => Detect(bytes, extension));
    }
    [Fact]
    public void ArbitraryZipLegacyAndTruncatedOfficeFilesAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => Detect(Encoding.ASCII.GetBytes("PK not a document"), ".docx"));
        Assert.Throws<InvalidDataException>(() => Detect(Encoding.ASCII.GetBytes("legacy"), ".doc"));
        var bytes = Package(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        Assert.Throws<InvalidDataException>(() => Detect(bytes[..(bytes.Length / 2)], ".docx"));
    }
    [Fact]
    public void RtfAndTiffRequireBoundedStructureInsteadOfJustAnExtension()
    {
        Assert.Equal("application/rtf", Detect(Encoding.ASCII.GetBytes("{\\rtf1 Hello {\\b World}}"), ".rtf"));
        Assert.Throws<InvalidDataException>(() => Detect(Encoding.ASCII.GetBytes("{\\rtf1 {\\object hidden}}"), ".rtf"));
        Assert.Throws<InvalidDataException>(() => Detect(Encoding.ASCII.GetBytes("{\\rtf1 unclosed"), ".rtf"));
        var tiff = new byte[87]; tiff[0] = (byte)'I'; tiff[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(2), 42); BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(4), 8); BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(8), 6);
        var tags = new[] { (256, 1), (257, 1), (273, 86), (279, 1), (258, 8), (259, 1) };
        for (var i = 0; i < tags.Length; i++)
        {
            var position = 10 + i * 12; BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(position), (ushort)tags[i].Item1);
            BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(position + 2), 4); BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(position + 4), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(position + 8), (uint)tags[i].Item2);
        }
        Assert.Equal("image/tiff", Detect(tiff, ".tiff"));
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(10 + 2 * 12 + 8), 9999);
        Assert.Throws<InvalidDataException>(() => Detect(tiff, ".tif"));
    }
    private static string Detect(byte[] bytes, string extension) => WorkflowBuiltInMediaValidator.Detect(bytes, extension, TestContext.Current.CancellationToken);
    private static byte[] Package(string extension, string mime, string? attack = null)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Entry(string path, string text) { using var writer = new StreamWriter(zip.CreateEntry(path, CompressionLevel.Optimal).Open(), new UTF8Encoding(false)); writer.Write(text); }
            if (extension is ".odt" or ".ods" or ".odp")
            {
                Entry("mimetype", mime);
                Entry("META-INF/manifest.xml", $"<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\"><manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"{mime}\"/></manifest:manifest>");
                var kind = extension == ".odt" ? "text" : extension == ".ods" ? "spreadsheet" : "presentation";
                Entry("content.xml", $"<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"><office:body><office:{kind}/></office:body></office:document-content>");
            }
            else
            {
                var path = extension == ".docx" ? "word/document.xml" : extension == ".xlsx" ? "xl/workbook.xml" : "ppt/presentation.xml";
                var family = extension == ".docx" ? "wordprocessingml" : extension == ".xlsx" ? "spreadsheetml" : "presentationml";
                var root = extension == ".docx" ? "document" : extension == ".xlsx" ? "workbook" : "presentation";
                Entry("[Content_Types].xml", $"<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/{path}\" ContentType=\"{mime}.main+xml\"/></Types>");
                Entry("_rels/.rels", $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"main\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"{path}\"/></Relationships>");
                var content = $"<{root} xmlns=\"http://schemas.openxmlformats.org/{family}/2006/main\"/>";
                if (attack == "dtd") content = "<!DOCTYPE document [<!ENTITY secret SYSTEM 'file:///etc/passwd'>]><document/>";
                if (attack == "depth") content = string.Concat(Enumerable.Repeat("<a>", 70)) + string.Concat(Enumerable.Repeat("</a>", 70));
                Entry(path, content);
            }
            if (attack == "odf-macro") Entry("Basic/Module.xml", "<module/>");
            if (attack == "macro") Entry("word/vbaProject.bin", "macro");
            if (attack == "traversal") Entry("../outside.txt", "outside");
            if (attack == "ratio") Entry("compressed.bin", new string('a', 3 * 1024 * 1024));
        }
        return output.ToArray();
    }
}
