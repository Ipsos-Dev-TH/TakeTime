using System;
using System.Collections.Generic;
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
                    CreateExcelFileStructure(tempDir, dataTable, reportTitle, sheetName);

                    string tempExcelPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
                    ZipFile.CreateFromDirectory(tempDir, tempExcelPath);

                    response.TransmitFile(tempExcelPath);
                    response.Flush();

                    File.Delete(tempExcelPath);
                }
                finally
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"เกิดข้อผิดพลาดในการสร้างไฟล์ Excel: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Create Excel file without report title row (data starts from row 1)
        /// </summary>
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
                    CreateExcelFileStructureNoTitle(tempDir, dataTable, sheetName);

                    string tempExcelPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
                    ZipFile.CreateFromDirectory(tempDir, tempExcelPath);

                    response.TransmitFile(tempExcelPath);
                    response.Flush();

                    File.Delete(tempExcelPath);
                }
                finally
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"เกิดข้อผิดพลาดในการสร้างไฟล์ Excel: {ex.Message}", ex);
            }
        }

        private static void CreateExcelFileStructure(string tempDir, DataTable dataTable, string reportTitle, string sheetName = "รายงาน")
        {
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

            CreateContentTypesXml(tempDir);
            CreateRelsFiles(relsDir, xlRelsDir);
            CreateDocProps(docPropsDir);
            CreateWorkbookAndStyles(xlDir, sheetName);
            CreateWorksheet(xlWorksheetsDir, dataTable, reportTitle);
        }

        private static void CreateExcelFileStructureNoTitle(string tempDir, DataTable dataTable, string sheetName)
        {
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

            CreateContentTypesXml(tempDir);
            CreateRelsFiles(relsDir, xlRelsDir);
            CreateDocProps(docPropsDir);
            CreateWorkbookAndStyles(xlDir, sheetName);
            CreateWorksheetNoTitle(xlWorksheetsDir, dataTable);
        }

        private static void CreateContentTypesXml(string tempDir)
        {
            string contentTypes = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
<Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
<Default Extension=""xml"" ContentType=""application/xml""/>
<Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
<Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
<Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>
<Override PartName=""/docProps/core.xml"" ContentType=""application/vnd.openxmlformats-package.core-properties+xml""/>
<Override PartName=""/docProps/app.xml"" ContentType=""application/vnd.openxmlformats-officedocument.extended-properties+xml""/>
</Types>";

            File.WriteAllText(Path.Combine(tempDir, "[Content_Types].xml"), contentTypes, Encoding.UTF8);
        }

        private static void CreateRelsFiles(string relsDir, string xlRelsDir)
        {
            // Root .rels
            string rootRels = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
<Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"" Target=""docProps/core.xml""/>
<Relationship Id=""rId3"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties"" Target=""docProps/app.xml""/>
</Relationships>";

            File.WriteAllText(Path.Combine(relsDir, ".rels"), rootRels, Encoding.UTF8);

            // Workbook .rels
            string workbookRels = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
<Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
</Relationships>";

            File.WriteAllText(Path.Combine(xlRelsDir, "workbook.xml.rels"), workbookRels, Encoding.UTF8);
        }

        private static void CreateDocProps(string docPropsDir)
        {
            // app.xml
            string appXml = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Properties xmlns=""http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"" xmlns:vt=""http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes"">
<Application>Microsoft Excel</Application>
<DocSecurity>0</DocSecurity>
<ScaleCrop>false</ScaleCrop>
<HeadingPairs>
<vt:vector size=""2"" baseType=""variant"">
<vt:variant><vt:lpstr>Worksheets</vt:lpstr></vt:variant>
<vt:variant><vt:i4>1</vt:i4></vt:variant>
</vt:vector>
</HeadingPairs>
<TitlesOfParts>
<vt:vector size=""1"" baseType=""lpstr"">
<vt:lpstr>Sheet1</vt:lpstr>
</vt:vector>
</TitlesOfParts>
<Company></Company>
<LinksUpToDate>false</LinksUpToDate>
<SharedDoc>false</SharedDoc>
<HyperlinksChanged>false</HyperlinksChanged>
<AppVersion>16.0300</AppVersion>
</Properties>";

            File.WriteAllText(Path.Combine(docPropsDir, "app.xml"), appXml, Encoding.UTF8);

            // core.xml
            string coreXml = $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<cp:coreProperties xmlns:cp=""http://schemas.openxmlformats.org/package/2006/metadata/core-properties"" xmlns:dc=""http://purl.org/dc/elements/1.1/"" xmlns:dcterms=""http://purl.org/dc/terms/"" xmlns:dcmitype=""http://purl.org/dc/dcmitype/"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
<dc:creator>TakeTime System</dc:creator>
<cp:lastModifiedBy>TakeTime System</cp:lastModifiedBy>
<dcterms:created xsi:type=""dcterms:W3CDTF"">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}Z</dcterms:created>
<dcterms:modified xsi:type=""dcterms:W3CDTF"">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}Z</dcterms:modified>
</cp:coreProperties>";

            File.WriteAllText(Path.Combine(docPropsDir, "core.xml"), coreXml, Encoding.UTF8);
        }

        private static void CreateWorkbookAndStyles(string xlDir, string sheetName = "รายงาน")
        {
            // workbook.xml
            string workbookXml = $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
<fileVersion appName=""xl"" lastEdited=""5"" lowestEdited=""5"" rupBuild=""9303""/>
<workbookPr defaultThemeVersion=""124226""/>
<bookViews>
<workbookView xWindow=""0"" yWindow=""0"" windowWidth=""20490"" windowHeight=""7755""/>
</bookViews>
<sheets>
<sheet name=""{EscapeXml(sheetName)}"" sheetId=""1"" r:id=""rId1""/>
</sheets>
<calcPr calcId=""145621""/>
</workbook>";

            File.WriteAllText(Path.Combine(xlDir, "workbook.xml"), workbookXml, Encoding.UTF8);

            // styles.xml
            string stylesXml = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
<fonts count=""2"">
<font><sz val=""11""/><color theme=""1""/><name val=""Tahoma""/><family val=""2""/><charset val=""222""/></font>
<font><b/><sz val=""11""/><color theme=""1""/><name val=""Tahoma""/><family val=""2""/><charset val=""222""/></font>
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
<cellXfs count=""3"">
<xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/>
<xf numFmtId=""0"" fontId=""1"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1""/>
<xf numFmtId=""2"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0"" applyNumberFormat=""1""/>
</cellXfs>
<cellStyles count=""1"">
<cellStyle name=""Normal"" xfId=""0"" builtinId=""0""/>
</cellStyles>
<dxfs count=""0""/>
<tableStyles count=""0"" defaultTableStyle=""TableStyleMedium2"" defaultPivotStyle=""PivotStyleLight16""/>
</styleSheet>";

            File.WriteAllText(Path.Combine(xlDir, "styles.xml"), stylesXml, Encoding.UTF8);
        }

        private static void CreateWorksheet(string xlWorksheetsDir, DataTable dataTable, string reportTitle)
        {
            var sb = new StringBuilder();
            sb.Append(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
            sb.Append(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">");

            // Calculate dimension
            int totalRows = dataTable.Rows.Count + 3; // title + empty + header + data
            int totalCols = dataTable.Columns.Count > 0 ? dataTable.Columns.Count : 1;
            string lastCol = GetExcelColumnName(totalCols);
            sb.Append($@"<dimension ref=""A1:{lastCol}{totalRows}""/>");

            sb.Append(@"<sheetViews><sheetView tabSelected=""1"" workbookViewId=""0""><selection activeCell=""A1"" sqref=""A1""/></sheetView></sheetViews>");
            sb.Append(@"<sheetFormatPr defaultRowHeight=""15""/>");
            sb.Append(@"<sheetData>");

            int currentRow = 1;

            // Title row (row 1)
            sb.Append($@"<row r=""{currentRow}"" ht=""20"">");
            sb.Append($@"<c r=""A{currentRow}"" s=""1"" t=""inlineStr""><is><t>{EscapeXml(reportTitle)}</t></is></c>");
            sb.Append(@"</row>");
            currentRow++;

            // Empty row (row 2)
            sb.Append($@"<row r=""{currentRow}""/>");
            currentRow++;

            if (dataTable.Columns.Count > 0)
            {
                // Headers (row 3)
                sb.Append($@"<row r=""{currentRow}"" ht=""18"">");
                for (int i = 0; i < dataTable.Columns.Count; i++)
                {
                    string cellRef = GetExcelColumnName(i + 1) + currentRow;
                    string columnName = dataTable.Columns[i].ColumnName;
                    sb.Append($@"<c r=""{cellRef}"" s=""1"" t=""inlineStr""><is><t>{EscapeXml(columnName)}</t></is></c>");
                }
                sb.Append(@"</row>");
                currentRow++;

                // Data rows
                for (int rowIndex = 0; rowIndex < dataTable.Rows.Count; rowIndex++)
                {
                    var row = dataTable.Rows[rowIndex];
                    sb.Append($@"<row r=""{currentRow}"">");

                    for (int colIndex = 0; colIndex < dataTable.Columns.Count; colIndex++)
                    {
                        string cellRef = GetExcelColumnName(colIndex + 1) + currentRow;
                        object cellValue = row[colIndex];

                        if (cellValue == null || cellValue == DBNull.Value)
                        {
                            sb.Append($@"<c r=""{cellRef}"" t=""inlineStr""><is><t></t></is></c>");
                        }
                        else if (cellValue is decimal || cellValue is double || cellValue is float || cellValue is int || cellValue is long)
                        {
                            sb.Append($@"<c r=""{cellRef}"" s=""2""><v>{cellValue}</v></c>");
                        }
                        else
                        {
                            string strValue = cellValue.ToString();
                            sb.Append($@"<c r=""{cellRef}"" t=""inlineStr""><is><t>{EscapeXml(strValue)}</t></is></c>");
                        }
                    }

                    sb.Append(@"</row>");
                    currentRow++;
                }
            }
            else
            {
                sb.Append($@"<row r=""{currentRow}""><c r=""A{currentRow}"" t=""inlineStr""><is><t>ไม่มีข้อมูล</t></is></c></row>");
            }

            sb.Append(@"</sheetData>");

            // Add merge cells for title row
            if (dataTable.Columns.Count > 1)
            {
                sb.Append(@"<mergeCells count=""1"">");
                sb.Append($@"<mergeCell ref=""A1:{GetExcelColumnName(dataTable.Columns.Count)}1""/>");
                sb.Append(@"</mergeCells>");
            }

            sb.Append(@"</worksheet>");

            File.WriteAllText(Path.Combine(xlWorksheetsDir, "sheet1.xml"), sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Create worksheet without title row - headers start from row 1
        /// </summary>
        private static void CreateWorksheetNoTitle(string xlWorksheetsDir, DataTable dataTable)
        {
            var sb = new StringBuilder();
            sb.Append(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
            sb.Append(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">");

            // Calculate dimension
            int totalRows = dataTable.Rows.Count + 1; // header + data
            int totalCols = dataTable.Columns.Count > 0 ? dataTable.Columns.Count : 1;
            string lastCol = GetExcelColumnName(totalCols);
            sb.Append($@"<dimension ref=""A1:{lastCol}{totalRows}""/>");

            sb.Append(@"<sheetViews><sheetView tabSelected=""1"" workbookViewId=""0""><selection activeCell=""A1"" sqref=""A1""/></sheetView></sheetViews>");
            sb.Append(@"<sheetFormatPr defaultRowHeight=""15""/>");
            sb.Append(@"<sheetData>");

            int currentRow = 1;

            if (dataTable.Columns.Count > 0)
            {
                // Headers (row 1)
                sb.Append($@"<row r=""{currentRow}"" ht=""18"">");
                for (int i = 0; i < dataTable.Columns.Count; i++)
                {
                    string cellRef = GetExcelColumnName(i + 1) + currentRow;
                    string columnName = dataTable.Columns[i].ColumnName;
                    sb.Append($@"<c r=""{cellRef}"" s=""1"" t=""inlineStr""><is><t>{EscapeXml(columnName)}</t></is></c>");
                }
                sb.Append(@"</row>");
                currentRow++;

                // Data rows
                for (int rowIndex = 0; rowIndex < dataTable.Rows.Count; rowIndex++)
                {
                    var row = dataTable.Rows[rowIndex];
                    sb.Append($@"<row r=""{currentRow}"">");

                    for (int colIndex = 0; colIndex < dataTable.Columns.Count; colIndex++)
                    {
                        string cellRef = GetExcelColumnName(colIndex + 1) + currentRow;
                        object cellValue = row[colIndex];

                        if (cellValue == null || cellValue == DBNull.Value)
                        {
                            sb.Append($@"<c r=""{cellRef}"" t=""inlineStr""><is><t></t></is></c>");
                        }
                        else if (cellValue is decimal || cellValue is double || cellValue is float || cellValue is int || cellValue is long)
                        {
                            sb.Append($@"<c r=""{cellRef}"" s=""2""><v>{cellValue}</v></c>");
                        }
                        else
                        {
                            string strValue = cellValue.ToString();
                            sb.Append($@"<c r=""{cellRef}"" t=""inlineStr""><is><t>{EscapeXml(strValue)}</t></is></c>");
                        }
                    }

                    sb.Append(@"</row>");
                    currentRow++;
                }
            }
            else
            {
                sb.Append($@"<row r=""{currentRow}""><c r=""A{currentRow}"" t=""inlineStr""><is><t>ไม่มีข้อมูล</t></is></c></row>");
            }

            sb.Append(@"</sheetData>");
            sb.Append(@"</worksheet>");

            File.WriteAllText(Path.Combine(xlWorksheetsDir, "sheet1.xml"), sb.ToString(), Encoding.UTF8);
        }

        private static string GetExcelColumnName(int columnNumber)
        {
            string columnName = "";
            while (columnNumber > 0)
            {
                int modulo = (columnNumber - 1) % 26;
                columnName = Convert.ToChar('A' + modulo) + columnName;
                columnNumber = (columnNumber - modulo) / 26;
            }
            return columnName;
        }

        private static string EscapeXml(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Replace("&", "&amp;")
                       .Replace("<", "&lt;")
                       .Replace(">", "&gt;")
                       .Replace("\"", "&quot;")
                       .Replace("'", "&apos;");
        }
    }
}
