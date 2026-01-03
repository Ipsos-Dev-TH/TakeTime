using System;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Web;

namespace Take_Time_BangPhra.Admin
{
    public class ExcelCreator
    {
        public static void CreateExcelFile(DataTable dataTable, string fileName, string reportTitle, HttpResponse response)
        {
            CreateExcelFile(dataTable, fileName, reportTitle, "รายงาน", response);
        }

        public static void CreateExcelFile(DataTable dataTable, string fileName, string reportTitle, string sheetName, HttpResponse response)
        {
            try
            {
                response.Clear();
                response.Buffer = true;
                response.AddHeader("content-disposition", $"attachment;filename={fileName}.xlsx");
                response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                response.Charset = "UTF-8";

                string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                try
                {
                    CreateExcelStructure(tempDir, dataTable, sheetName, reportTitle, true);

                    string tempExcelPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
                    ZipFile.CreateFromDirectory(tempDir, tempExcelPath);

                    response.TransmitFile(tempExcelPath);
                    response.Flush();

                    try { File.Delete(tempExcelPath); } catch { }
                }
                finally
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"เกิดข้อผิดพลาดในการสร้างไฟล์ Excel: {ex.Message}", ex);
            }
        }

        public static void CreateExcelFileNoTitle(DataTable dataTable, string fileName, string sheetName, HttpResponse response)
        {
            try
            {
                response.Clear();
                response.Buffer = true;
                response.AddHeader("content-disposition", $"attachment;filename={fileName}.xlsx");
                response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                response.Charset = "UTF-8";

                string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                try
                {
                    CreateExcelStructure(tempDir, dataTable, sheetName, null, false);

                    string tempExcelPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
                    ZipFile.CreateFromDirectory(tempDir, tempExcelPath);

                    response.TransmitFile(tempExcelPath);
                    response.Flush();

                    try { File.Delete(tempExcelPath); } catch { }
                }
                finally
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"เกิดข้อผิดพลาดในการสร้างไฟล์ Excel: {ex.Message}", ex);
            }
        }

        private static void CreateExcelStructure(string tempDir, DataTable dataTable, string sheetName, string reportTitle, bool hasTitle)
        {
            // Create directory structure
            string relsDir = Path.Combine(tempDir, "_rels");
            string docPropsDir = Path.Combine(tempDir, "docProps");
            string xlDir = Path.Combine(tempDir, "xl");
            string xlRelsDir = Path.Combine(xlDir, "_rels");
            string xlWorksheetsDir = Path.Combine(xlDir, "worksheets");

            Directory.CreateDirectory(relsDir);
            Directory.CreateDirectory(docPropsDir);
            Directory.CreateDirectory(xlDir);
            Directory.CreateDirectory(xlRelsDir);
            Directory.CreateDirectory(xlWorksheetsDir);

            // [Content_Types].xml
            File.WriteAllText(
                Path.Combine(tempDir, "[Content_Types].xml"),
                @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
<Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
<Default Extension=""xml"" ContentType=""application/xml""/>
<Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
<Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
<Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>
<Override PartName=""/xl/sharedStrings.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml""/>
<Override PartName=""/docProps/core.xml"" ContentType=""application/vnd.openxmlformats-package.core-properties+xml""/>
<Override PartName=""/docProps/app.xml"" ContentType=""application/vnd.openxmlformats-officedocument.extended-properties+xml""/>
</Types>",
                Encoding.UTF8);

            // _rels/.rels
            File.WriteAllText(
                Path.Combine(relsDir, ".rels"),
                @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
<Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"" Target=""docProps/core.xml""/>
<Relationship Id=""rId3"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties"" Target=""docProps/app.xml""/>
</Relationships>",
                Encoding.UTF8);

            // xl/_rels/workbook.xml.rels
            File.WriteAllText(
                Path.Combine(xlRelsDir, "workbook.xml.rels"),
                @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
<Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
<Relationship Id=""rId3"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings"" Target=""sharedStrings.xml""/>
</Relationships>",
                Encoding.UTF8);

            // docProps/app.xml
            File.WriteAllText(
                Path.Combine(docPropsDir, "app.xml"),
                @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Properties xmlns=""http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"">
<Application>Microsoft Excel</Application>
<AppVersion>16.0300</AppVersion>
</Properties>",
                Encoding.UTF8);

            // docProps/core.xml
            File.WriteAllText(
                Path.Combine(docPropsDir, "core.xml"),
                $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<cp:coreProperties xmlns:cp=""http://schemas.openxmlformats.org/package/2006/metadata/core-properties"" xmlns:dc=""http://purl.org/dc/elements/1.1/"" xmlns:dcterms=""http://purl.org/dc/terms/"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
<dc:creator>TakeTime</dc:creator>
<dcterms:created xsi:type=""dcterms:W3CDTF"">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}Z</dcterms:created>
</cp:coreProperties>",
                Encoding.UTF8);

            // xl/workbook.xml
            File.WriteAllText(
                Path.Combine(xlDir, "workbook.xml"),
                $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
<sheets>
<sheet name=""{EscapeXml(sheetName)}"" sheetId=""1"" r:id=""rId1""/>
</sheets>
</workbook>",
                Encoding.UTF8);

            // xl/styles.xml
            File.WriteAllText(
                Path.Combine(xlDir, "styles.xml"),
                @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
<fonts count=""2"">
<font><sz val=""11""/><name val=""Tahoma""/></font>
<font><b/><sz val=""11""/><name val=""Tahoma""/></font>
</fonts>
<fills count=""2"">
<fill><patternFill patternType=""none""/></fill>
<fill><patternFill patternType=""gray125""/></fill>
</fills>
<borders count=""1"">
<border><left/><right/><top/><bottom/><diagonal/></border>
</borders>
<cellStyleXfs count=""1"">
<xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/>
</cellStyleXfs>
<cellXfs count=""2"">
<xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/>
<xf numFmtId=""0"" fontId=""1"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1""/>
</cellXfs>
</styleSheet>",
                Encoding.UTF8);

            // Build shared strings and worksheet
            var sharedStrings = new System.Collections.Generic.List<string>();
            var sb = new StringBuilder();

            sb.Append(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
            sb.Append(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">");
            sb.Append(@"<sheetData>");

            int rowNum = 1;
            int colCount = dataTable.Columns.Count > 0 ? dataTable.Columns.Count : 1;

            // Title row (if hasTitle)
            if (hasTitle && !string.IsNullOrEmpty(reportTitle))
            {
                sb.Append($@"<row r=""{rowNum}"">");
                int ssIndex = AddSharedString(sharedStrings, reportTitle);
                sb.Append($@"<c r=""A{rowNum}"" t=""s"" s=""1""><v>{ssIndex}</v></c>");
                sb.Append(@"</row>");
                rowNum++;

                // Empty row
                sb.Append($@"<row r=""{rowNum}""/>");
                rowNum++;
            }

            if (dataTable.Columns.Count > 0)
            {
                // Header row
                sb.Append($@"<row r=""{rowNum}"">");
                for (int c = 0; c < dataTable.Columns.Count; c++)
                {
                    string colName = GetColName(c);
                    int ssIndex = AddSharedString(sharedStrings, dataTable.Columns[c].ColumnName);
                    sb.Append($@"<c r=""{colName}{rowNum}"" t=""s"" s=""1""><v>{ssIndex}</v></c>");
                }
                sb.Append(@"</row>");
                rowNum++;

                // Data rows
                foreach (DataRow row in dataTable.Rows)
                {
                    sb.Append($@"<row r=""{rowNum}"">");
                    for (int c = 0; c < dataTable.Columns.Count; c++)
                    {
                        string colName = GetColName(c);
                        object val = row[c];

                        if (val == null || val == DBNull.Value)
                        {
                            // Empty cell
                            sb.Append($@"<c r=""{colName}{rowNum}""/>");
                        }
                        else if (IsNumeric(val))
                        {
                            // Numeric value
                            sb.Append($@"<c r=""{colName}{rowNum}""><v>{Convert.ToDecimal(val)}</v></c>");
                        }
                        else
                        {
                            // String value
                            int ssIndex = AddSharedString(sharedStrings, val.ToString());
                            sb.Append($@"<c r=""{colName}{rowNum}"" t=""s""><v>{ssIndex}</v></c>");
                        }
                    }
                    sb.Append(@"</row>");
                    rowNum++;
                }
            }

            sb.Append(@"</sheetData>");

            // Merge cells for title
            if (hasTitle && !string.IsNullOrEmpty(reportTitle) && dataTable.Columns.Count > 1)
            {
                string lastCol = GetColName(dataTable.Columns.Count - 1);
                sb.Append($@"<mergeCells count=""1""><mergeCell ref=""A1:{lastCol}1""/></mergeCells>");
            }

            sb.Append(@"</worksheet>");

            // Write worksheet
            File.WriteAllText(Path.Combine(xlWorksheetsDir, "sheet1.xml"), sb.ToString(), Encoding.UTF8);

            // Write sharedStrings.xml
            var ssSb = new StringBuilder();
            ssSb.Append(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
            ssSb.Append($@"<sst xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" count=""{sharedStrings.Count}"" uniqueCount=""{sharedStrings.Count}"">");
            foreach (string s in sharedStrings)
            {
                ssSb.Append($@"<si><t>{EscapeXml(s)}</t></si>");
            }
            ssSb.Append(@"</sst>");
            File.WriteAllText(Path.Combine(xlDir, "sharedStrings.xml"), ssSb.ToString(), Encoding.UTF8);
        }

        private static int AddSharedString(System.Collections.Generic.List<string> list, string value)
        {
            string val = value ?? "";
            int index = list.IndexOf(val);
            if (index >= 0) return index;
            list.Add(val);
            return list.Count - 1;
        }

        private static string GetColName(int colIndex)
        {
            string result = "";
            int n = colIndex + 1;
            while (n > 0)
            {
                n--;
                result = (char)('A' + n % 26) + result;
                n /= 26;
            }
            return result;
        }

        private static bool IsNumeric(object val)
        {
            return val is decimal || val is double || val is float ||
                   val is int || val is long || val is short ||
                   val is byte || val is uint || val is ulong || val is ushort;
        }

        private static string EscapeXml(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
