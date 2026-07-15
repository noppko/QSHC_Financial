using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace Financial.Helpers
{
    /// <summary>
    /// Helper สำหรับสร้าง Excel ด้วย EPPlus
    /// </summary>
    public static class ExcelHelper
    {
        static ExcelHelper()
        {
            // ตั้งค่า EPPlus License Context
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>
        /// สร้าง Excel file จาก data พร้อม header
        /// </summary>
        public static byte[] CreateExcel<T>(List<T> data, string sheetName, Dictionary<string, Func<T, object>> columns)
        {
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add(sheetName);

            // สร้าง Header
            int col = 1;
            foreach (var column in columns)
            {
                var headerCell = worksheet.Cells[1, col];
                headerCell.Value = column.Key;
                headerCell.Style.Font.Bold = true;
                headerCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                headerCell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                headerCell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                col++;
            }

            // เพิ่มข้อมูล
            int row = 2;
            foreach (var item in data)
            {
                col = 1;
                foreach (var column in columns)
                {
                    var cell = worksheet.Cells[row, col];
                    cell.Value = column.Value(item);
                    cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    col++;
                }
                row++;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            return package.GetAsByteArray();
        }
    }
}
