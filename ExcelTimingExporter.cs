using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Windows.Media;
using System.Xml;

namespace TimingTableCalculator;

internal static class ExcelTimingExporter
{
    public static void Export(string path, double[] rpm, double[] map, double[,] timing, string mapUnit,
        Color lowColor, Color middleColor, Color highColor, bool twoColorScale,
        string sheetName = "Timing Map", string documentTitle = "Ignition Timing Map", string xAxisTitle = "Engine RPM")
    {
        using var output = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create);
        WriteEntry(archive, "[Content_Types].xml", ContentTypes);
        WriteEntry(archive, "_rels/.rels", PackageRelationships);
        WriteEntry(archive, "xl/workbook.xml", BuildWorkbook(sheetName));
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships);
        WriteEntry(archive, "xl/styles.xml", Styles);
        WriteEntry(archive, "docProps/app.xml", AppProperties);
        WriteEntry(archive, "docProps/core.xml", BuildCoreProperties(documentTitle));
        WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildSheet(rpm, map, timing, mapUnit, lowColor, middleColor, highColor, twoColorScale, xAxisTitle));
    }

    private static string BuildSheet(double[] rpm, double[] map, double[,] timing, string mapUnit,
        Color low, Color middle, Color high, bool twoColor, string xAxisTitle)
    {
        var lastColumn = ColumnName(rpm.Length + 1);
        var lastRow = map.Length + 1;
        var dataRange = $"B1:{lastColumn}{map.Length}";
        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetViews><sheetView workbookViewId=\"0\"><pane xSplit=\"1\" topLeftCell=\"B1\" activePane=\"topRight\" state=\"frozen\"/></sheetView></sheetViews><cols><col min=\"1\" max=\"1\" width=\"20\" customWidth=\"1\"/><col min=\"2\" max=\"")
            .Append(rpm.Length + 1).Append("\" width=\"10\" customWidth=\"1\"/></cols><sheetData>");

        for (var row = 0; row < map.Length; row++)
        {
            var excelRow = row + 1;
            xml.Append("<row r=\"").Append(excelRow).Append("\">");
            NumberCell(xml, $"A{excelRow}", map[row], 1);
            for (var col = 0; col < rpm.Length; col++)
                NumberCell(xml, $"{ColumnName(col + 2)}{excelRow}", timing[row, col], 0);
            xml.Append("</row>");
        }
        xml.Append("<row r=\"").Append(lastRow).Append("\"><c r=\"A").Append(lastRow).Append("\" t=\"inlineStr\" s=\"1\"><is><t>").Append(Escape(xAxisTitle)).Append("</t></is></c>");
        for (var col = 0; col < rpm.Length; col++) NumberCell(xml, $"{ColumnName(col + 2)}{lastRow}", rpm[col], 1);
        xml.Append("</row></sheetData><conditionalFormatting sqref=\"").Append(dataRange).Append("\"><cfRule type=\"colorScale\" priority=\"1\"><colorScale>");
        if (twoColor)
        {
            xml.Append("<cfvo type=\"min\"/><cfvo type=\"max\"/><color rgb=\"").Append(Argb(low)).Append("\"/><color rgb=\"").Append(Argb(high)).Append("\"/>");
        }
        else
        {
            xml.Append("<cfvo type=\"min\"/><cfvo type=\"percentile\" val=\"50\"/><cfvo type=\"max\"/><color rgb=\"").Append(Argb(low)).Append("\"/><color rgb=\"").Append(Argb(middle)).Append("\"/><color rgb=\"").Append(Argb(high)).Append("\"/>");
        }
        xml.Append("</colorScale></cfRule></conditionalFormatting><pageMargins left=\"0.25\" right=\"0.25\" top=\"0.5\" bottom=\"0.5\" header=\"0.2\" footer=\"0.2\"/></worksheet>");
        return xml.ToString();
    }

    private static void NumberCell(StringBuilder xml, string reference, double value, int style) =>
        xml.Append("<c r=\"").Append(reference).Append("\" s=\"").Append(style).Append("\"><v>")
            .Append(value.ToString("0.###############", CultureInfo.InvariantCulture)).Append("</v></c>");

    private static string ColumnName(int column)
    {
        var result = "";
        while (column > 0) { column--; result = (char)('A' + column % 26) + result; column /= 26; }
        return result;
    }

    private static string Argb(Color color) => $"FF{color.R:X2}{color.G:X2}{color.B:X2}";
    private static string BuildWorkbook(string sheetName) => $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"{Escape(sheetName)}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";
    private static string BuildCoreProperties(string title) => $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:title>{Escape(title)}</dc:title><dc:creator>Map Lab</dc:creator></cp:coreProperties>";
    private static string Escape(string value) => SecurityElement.Escape(value) ?? "";
    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private const string ContentTypes = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/><Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/><Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/></Types>";
    private const string PackageRelationships = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/><Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/></Relationships>";
    private const string WorkbookRelationships = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>";
    private const string Styles = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"0.0\"/></numFmts><fonts count=\"2\"><font><sz val=\"10\"/><name val=\"Segoe UI\"/></font><font><b/><color rgb=\"FFFFFFFF\"/><sz val=\"10\"/><name val=\"Segoe UI\"/></font></fonts><fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF121A26\"/><bgColor indexed=\"64\"/></patternFill></fill></fills><borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"2\"><xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"><alignment horizontal=\"center\"/></xf><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\"><alignment horizontal=\"center\"/></xf></cellXfs></styleSheet>";
    private const string AppProperties = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\"><Application>Map Lab</Application></Properties>";
}
