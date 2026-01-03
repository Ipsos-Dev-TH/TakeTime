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

                // สร้าง temporary directory สำหรับโครงสร้าง Excel
                string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                try
                {
                    // สร้างโครงสร้างไฟล์ Excel
                    CreateExcelFileStructure(tempDir, dataTable, reportTitle, sheetName);

                    // สร้างไฟล์ .xlsx (ซึ่งคือ zip archive)
                    string tempExcelPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
                    ZipFile.CreateFromDirectory(tempDir, tempExcelPath);

                    // ส่งไฟล์ไปยัง client
                    response.TransmitFile(tempExcelPath);
                    response.Flush();

                    // ลบไฟล์ชั่วคราว
                    File.Delete(tempExcelPath);
                }
                finally
                {
                    // ลบ temporary directory
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
            // สร้าง directories ที่จำเป็น
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

            // สร้างไฟล์ [Content_Types].xml
            CreateContentTypesXml(tempDir);

            // สร้างไฟล์ .rels
            CreateRelsFiles(relsDir, xlRelsDir);

            // สร้าง docProps
            CreateDocProps(docPropsDir);

            // สร้าง workbook และ styles
            CreateWorkbookAndStyles(xlDir, sheetName);

            // สร้าง worksheet พร้อมข้อมูล
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
            string contentTypes = @"<?xml version='1.0' encoding='UTF-8' standalone='yes'?>
<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'>
    <Default Extension='rels' ContentType='application/vnd.openxmlformats-package.relationships+xml'/>
    <Default Extension='xml' ContentType='application/xml'/>
    <Override PartName='/xl/workbook.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml'/>
    <Override PartName='/xl/worksheets/sheet1.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml'/>
    <Override PartName='/xl/styles.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml'/>
    <Override PartName='/docProps/core.xml' ContentType='application/vnd.openxmlformats-package.core-properties+xml'/>
    <Override PartName='/docProps/app.xml' ContentType='application/vnd.openxmlformats-officedocument.extended-properties+xml'/>
</Types>";

            File.WriteAllText(Path.Combine(tempDir, "[Content_Types].xml"), contentTypes, Encoding.UTF8);
        }

        private static void CreateRelsFiles(string relsDir, string xlRelsDir)
        {
            // Root .rels
            string rootRels = @"<?xml version='1.0' encoding='UTF-8' standalone='yes'?>
<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'>
    <Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument' Target='xl/workbook.xml'/>
    <Relationship Id='rId2' Type='http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties' Target='docProps/core.xml'/>
    <Relationship Id='rId3' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties' Target='docProps/app.xml'/>
</Relationships>";

            File.WriteAllText(Path.Combine(relsDir, ".rels"), rootRels, Encoding.UTF8);

            // Workbook .rels
            string workbookRels = @"<?xml version='1.0' encoding='UTF-8' standalone='yes'?>
<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'>
    <Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet' Target='worksheets/sheet1.xml'/>
    <Relationship Id='rId2' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles' Target='styles.xml'/>
</Relationships>";

            File.WriteAllText(Path.Combine(xlRelsDir, "workbook.xml.rels"), workbookRels, Encoding.UTF8);
        }

        private static void CreateDocProps(string docPropsDir)
        {
            // app.xml
            string appXml = @"<?xml version='1.0' encoding='UTF-8' standalone='yes'?>
<Properties xmlns='http://schemas.openxmlformats.org/officeDocument/2006/extended-properties'>
    <Application>Take Time BangPhra System</Application>
    <DocSecurity>0</DocSecurity>
    <ScaleCrop>false</ScaleCrop>
    <HeadingPairs>
        <vt:vector size='2' baseType='variant' xmlns:vt='http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes'>
            <vt:variant>
                <vt:lpstr>Worksheets</vt:lpstr>
            </vt:variant>
            <vt:variant>
                <vt:i4>1</vt:i4>
            </vt:variant>
        </vt:vector>
    </HeadingPairs>
    <TitlesOfParts>
        <vt:vector size='1' baseType='lpstr' xmlns:vt='http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes'>
            <vt:lpstr>Sheet1</vt:lpstr>
        </vt:vector>
    </TitlesOfParts>
    <Company>Take Time BangPhra</Company>
    <LinksUpToDate>false</LinksUpToDate>
    <SharedDoc>false</SharedDoc>
    <HyperlinksChanged>false</HyperlinksChanged>
    <AppVersion>16.0300</AppVersion>
</Properties>";

            File.WriteAllText(Path.Combine(docPropsDir, "app.xml"), appXml, Encoding.UTF8);

            // core.xml
            string coreXml = $@"<?xml version='1.0' encoding='UTF-8' standalone='yes'?>
<cp:coreProperties xmlns:cp='http://schemas.openxmlformats.org/package/2006/metadata/core-properties' xmlns:dc='http://purl.org/dc/elements/1.1/' xmlns:dcterms='http://purl.org/dc/terms/' xmlns:dcmitype='http://purl.org/dc/dcmitype/' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'>
    <dc:creator>Take Time BangPhra System</dc:creator>
    <cp:lastModifiedBy>Take Time BangPhra System</cp:lastModifiedBy>
    <dcterms:created xsi:type='dcterms:W3CDTF'>{DateTime.Now:yyyy-MM-ddTHH:mm:ssZ}</dcterms:created>
    <dcterms:modified xsi:type='dcterms:W3CDTF'>{DateTime.Now:yyyy-MM-ddTHH:mm:ssZ}</dcterms:modified>
</cp:coreProperties>";

            File.WriteAllText(Path.Combine(docPropsDir, "core.xml"), coreXml, Encoding.UTF8);
        }

        private static void CreateWorkbookAndStyles(string xlDir, string sheetName = "รายงาน")
        {
            // workbook.xml
            string workbookXml = $@"<?xml version='1.0' encoding='UTF-8' standalone='yes'?>
<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'>
    <fileVersion appName='xl' lastEdited='5' lowestEdited='5' rupBuild='9303'/>
    <workbookPr defaultThemeVersion='124226'/>
    <bookViews>
        <workbookView xWindow='480' yWindow='60' windowWidth='18195' windowHeight='8505'/>
    </bookViews>
    <sheets>
        <sheet name='{EscapeXml(sheetName)}' sheetId='1' r:id='rId1'/>
    </sheets>
    <calcPr calcId='145621'/>
</workbook>";

            File.WriteAllText(Path.Combine(xlDir, "workbook.xml"), workbookXml, Encoding.UTF8);

            // styles.xml
            string stylesXml = @"<?xml version='1.0' encoding='UTF-8' standalone='yes'?>
<styleSheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'>
    <fonts count='2'>
        <font>
            <sz val='12'/>
            <color rgb='FF000000'/>
            <name val='Tahoma'/>
            <family val='2'/>
        </font>
        <font>
            <b/>
            <sz val='12'/>
            <color rgb='FF000000'/>
            <name val='Tahoma'/>
            <family val='2'/>
        </font>
    </fonts>
    <fills count='2'>
        <fill>
            <patternFill patternType='none'/>
        </fill>
        <fill>
            <patternFill patternType='gray125'/>
        </fill>
    </fills>
    <borders count='1'>
        <border>
            <left/>
            <right/>
            <top/>
            <bottom/>
            <diagonal/>
        </border>
    </borders>
    <cellStyleXfs count='1'>
        <xf numFmtId='0' fontId='0' fillId='0' borderId='0'/>
    </cellStyleXfs>
    <cellXfs count='3'>
        <xf numFmtId='0' fontId='0' fillId='0' borderId='0' xfId='0'/>
        <xf numFmtId='0' fontId='1' fillId='0' borderId='0' xfId='0' applyFont='1'/>
        <xf numFmtId='2' fontId='0' fillId='0' borderId='0' xfId='0' applyNumberFormat='1'/>
    </cellXfs>
    <cellStyles count='1'>
        <cellStyle name='Normal' xfId='0' builtinId='0'/>
    </cellStyles>
    <dxfs count='0'/>
    <tableStyles count='0' defaultTableStyle='TableStyleMedium2' defaultPivotStyle='PivotStyleLight16'/>
</styleSheet>";

            File.WriteAllText(Path.Combine(xlDir, "styles.xml"), stylesXml, Encoding.UTF8);
        }

        private static void CreateWorksheet(string xlWorksheetsDir, DataTable dataTable, string reportTitle)
        {
            var sb = new StringBuilder();
            sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
            sb.AppendLine(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">");

            // Sheet dimension ต้องอยู่หลัง sheetViews
            sb.AppendLine(@"<sheetViews>");
            sb.AppendLine(@"<sheetView tabSelected=""1"" workbookViewId=""0"">");
            sb.AppendLine(@"<selection activeCell=""A1"" sqref=""A1""/>");
            sb.AppendLine(@"</sheetView>");
            sb.AppendLine(@"</sheetViews>");

            sb.AppendLine(@"<sheetFormatPr defaultRowHeight=""15""/>");
            sb.AppendLine(@"<sheetData>");

            int currentRow = 1;

            // เพิ่มหัวข้อรายงาน (row 1)
            sb.AppendLine($@"<row r=""{currentRow}"" ht=""20"">");
            sb.AppendLine($@"<c r=""A{currentRow}"" s=""1"" t=""inlineStr""><is><t>{EscapeXml(reportTitle)}</t></is></c>");

            // Merge cells สำหรับหัวข้อ (ถ้าต้องการให้หัวข้ออยู่กลางหลายคอลัมน์)
            if (dataTable.Columns.Count > 1)
            {
                string lastColumn = GetExcelColumnName(dataTable.Columns.Count);
                sb.AppendLine($@"<mergeCells count=""1""><mergeCell ref=""A{currentRow}:{lastColumn}{currentRow}""/></mergeCells>");
            }

            sb.AppendLine(@"</row>");
            currentRow++;

            // เพิ่มบรรทัดว่าง
            sb.AppendLine($@"<row r=""{currentRow}"">");
            sb.AppendLine($@"<c r=""A{currentRow}"" t=""inlineStr""><is><t></t></is></c>");
            sb.AppendLine(@"</row>");
            currentRow++;

            if (dataTable.Rows.Count > 0 && dataTable.Columns.Count > 0)
            {
                // Headers (row 3)
                sb.AppendLine($@"<row r=""{currentRow}"" ht=""18"">");
                for (int i = 0; i < dataTable.Columns.Count; i++)
                {
                    string cellRef = GetExcelColumnName(i + 1) + currentRow;
                    string columnName = dataTable.Columns[i].ColumnName;
                    sb.AppendLine($@"<c r=""{cellRef}"" s=""1"" t=""inlineStr""><is><t>{EscapeXml(columnName)}</t></is></c>");
                }
                sb.AppendLine(@"</row>");
                currentRow++;

                // Data rows
                for (int rowIndex = 0; rowIndex < dataTable.Rows.Count; rowIndex++)
                {
                    var row = dataTable.Rows[rowIndex];
                    sb.AppendLine($@"<row r=""{currentRow}"">");

                    for (int colIndex = 0; colIndex < dataTable.Columns.Count; colIndex++)
                    {
                        string value = row[colIndex]?.ToString() ?? "";
                        string cellRef = GetExcelColumnName(colIndex + 1) + currentRow;

                        // ตรวจสอบว่าเป็นตัวเลขหรือไม่
                        if (decimal.TryParse(value.Replace(",", ""), out decimal numericValue) && numericValue != 0)
                        {
                            sb.AppendLine($@"<c r=""{cellRef}"" s=""2""><v>{numericValue}</v></c>");
                        }
                        else
                        {
                            sb.AppendLine($@"<c r=""{cellRef}"" t=""inlineStr""><is><t>{EscapeXml(value)}</t></is></c>");
                        }
                    }

                    sb.AppendLine(@"</row>");
                    currentRow++;
                }
            }
            else
            {
                // ถ้าไม่มีข้อมูล ให้แสดงข้อความว่าไม่มีข้อมูล
                sb.AppendLine($@"<row r=""{currentRow}"">");
                sb.AppendLine($@"<c r=""A{currentRow}"" t=""inlineStr""><is><t>ไม่มีข้อมูล</t></is></c>");
                sb.AppendLine(@"</row>");
            }

            sb.AppendLine(@"</sheetData>");

            // เพิ่ม mergeCells ถ้ามี
            if (dataTable.Columns.Count > 1)
            {
                sb.AppendLine(@"<mergeCells count=""1"">");
                sb.AppendLine($@"<mergeCell ref=""A1:{GetExcelColumnName(dataTable.Columns.Count)}1""/>");
                sb.AppendLine(@"</mergeCells>");
            }

            sb.AppendLine(@"</worksheet>");

            File.WriteAllText(Path.Combine(xlWorksheetsDir, "sheet1.xml"), sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Create worksheet without title row - headers start from row 1
        /// </summary>
        private static void CreateWorksheetNoTitle(string xlWorksheetsDir, DataTable dataTable)
        {
            var sb = new StringBuilder();
            sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
            sb.AppendLine(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">");

            sb.AppendLine(@"<sheetViews>");
            sb.AppendLine(@"<sheetView tabSelected=""1"" workbookViewId=""0"">");
            sb.AppendLine(@"<selection activeCell=""A1"" sqref=""A1""/>");
            sb.AppendLine(@"</sheetView>");
            sb.AppendLine(@"</sheetViews>");

            sb.AppendLine(@"<sheetFormatPr defaultRowHeight=""15""/>");
            sb.AppendLine(@"<sheetData>");

            int currentRow = 1;

            if (dataTable.Rows.Count > 0 && dataTable.Columns.Count > 0)
            {
                // Headers (row 1)
                sb.AppendLine($@"<row r=""{currentRow}"" ht=""18"">");
                for (int i = 0; i < dataTable.Columns.Count; i++)
                {
                    string cellRef = GetExcelColumnName(i + 1) + currentRow;
                    string columnName = dataTable.Columns[i].ColumnName;
                    sb.AppendLine($@"<c r=""{cellRef}"" s=""1"" t=""inlineStr""><is><t>{EscapeXml(columnName)}</t></is></c>");
                }
                sb.AppendLine(@"</row>");
                currentRow++;

                // Data rows
                for (int rowIndex = 0; rowIndex < dataTable.Rows.Count; rowIndex++)
                {
                    var row = dataTable.Rows[rowIndex];
                    sb.AppendLine($@"<row r=""{currentRow}"">");

                    for (int colIndex = 0; colIndex < dataTable.Columns.Count; colIndex++)
                    {
                        string value = row[colIndex]?.ToString() ?? "";
                        string cellRef = GetExcelColumnName(colIndex + 1) + currentRow;

                        // ตรวจสอบว่าเป็นตัวเลขหรือไม่
                        if (decimal.TryParse(value.Replace(",", ""), out decimal numericValue) && numericValue != 0)
                        {
                            sb.AppendLine($@"<c r=""{cellRef}"" s=""2""><v>{numericValue}</v></c>");
                        }
                        else
                        {
                            sb.AppendLine($@"<c r=""{cellRef}"" t=""inlineStr""><is><t>{EscapeXml(value)}</t></is></c>");
                        }
                    }

                    sb.AppendLine(@"</row>");
                    currentRow++;
                }
            }
            else
            {
                sb.AppendLine($@"<row r=""{currentRow}"">");
                sb.AppendLine($@"<c r=""A{currentRow}"" t=""inlineStr""><is><t>ไม่มีข้อมูล</t></is></c>");
                sb.AppendLine(@"</row>");
            }

            sb.AppendLine(@"</sheetData>");
            sb.AppendLine(@"</worksheet>");

            File.WriteAllText(Path.Combine(xlWorksheetsDir, "sheet1.xml"), sb.ToString(), Encoding.UTF8);
        }

        // Helper methods
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