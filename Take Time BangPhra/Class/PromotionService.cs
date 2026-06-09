using System;
using System.Collections.Generic;
using System.Data;

/// <summary>
/// จัดการโฆษณา/โปรโมชั่น — CRUD + ดึงรายการที่ active สำหรับเด้ง popup หน้าแรก
/// ใช้ตาราง Promotions (สร้างอัตโนมัติด้วย EnsureTableExists / migration 004)
/// </summary>
public class PromotionService
{
    private readonly code _code = new code();
    private readonly string _conn;

    public PromotionService(string connectionString)
    {
        _conn = connectionString;
    }

    public PromotionService()
    {
        _conn = System.Configuration.ConfigurationManager
            .ConnectionStrings["TaketimeConnectionString"].ConnectionString;
    }

    /// <summary>สร้างตารางถ้ายังไม่มี (กันกรณียังไม่ได้รัน migration)</summary>
    public void EnsureTableExists()
    {
        string sql = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Promotions' AND xtype='U')
            CREATE TABLE Promotions (
                ID INT IDENTITY(1,1) PRIMARY KEY,
                Title NVARCHAR(200) NOT NULL,
                Description NVARCHAR(MAX) NULL,
                Image_Url NVARCHAR(500) NULL,
                Link_Url NVARCHAR(500) NULL,
                Show_On_Homepage BIT NOT NULL DEFAULT 1,
                Start_Date DATETIME NULL,
                End_Date DATETIME NULL,
                Sort_Order INT NOT NULL DEFAULT 0,
                Status NVARCHAR(10) NOT NULL DEFAULT 'True',
                Created_Date DATETIME NOT NULL DEFAULT GETDATE(),
                Updated_Date DATETIME NULL
            )";
        _code.DatabaseInsertSafe(_conn, sql, new Dictionary<string, object>());
    }

    /// <summary>รายการทั้งหมด (สำหรับหน้า admin) เรียงตาม Sort_Order แล้ว ID</summary>
    public DataTable GetAll()
    {
        return _code.DatabaseQuerySafe(_conn,
            "SELECT * FROM Promotions ORDER BY Sort_Order ASC, ID DESC", null);
    }

    public DataRow GetById(int id)
    {
        var dt = _code.DatabaseQuerySafe(_conn,
            "SELECT * FROM Promotions WHERE ID = @ID",
            new Dictionary<string, object> { { "@ID", id } });
        return (dt != null && dt.Rows.Count > 0) ? dt.Rows[0] : null;
    }

    /// <summary>
    /// โปรโมชั่นที่ใช้งานอยู่และตั้งให้เด้งหน้าแรก ภายในช่วงเวลาที่กำหนด — สำหรับ popup หน้าแรก
    /// </summary>
    public DataTable GetActiveHomepagePromotions()
    {
        string sql = @"
            SELECT ID, Title, Description, Image_Url, Link_Url
            FROM Promotions
            WHERE Status = 'True'
              AND Show_On_Homepage = 1
              AND (Start_Date IS NULL OR Start_Date <= GETDATE())
              AND (End_Date IS NULL OR End_Date >= GETDATE())
            ORDER BY Sort_Order ASC, ID DESC";
        return _code.DatabaseQuerySafe(_conn, sql, null);
    }

    public int Insert(string title, string description, string imageUrl, string linkUrl,
        bool showOnHomepage, DateTime? startDate, DateTime? endDate, int sortOrder, bool active)
    {
        string sql = @"
            INSERT INTO Promotions
                (Title, Description, Image_Url, Link_Url, Show_On_Homepage,
                 Start_Date, End_Date, Sort_Order, Status, Created_Date)
            VALUES
                (@Title, @Description, @ImageUrl, @LinkUrl, @ShowHome,
                 @StartDate, @EndDate, @SortOrder, @Status, GETDATE())";
        return _code.DatabaseInsertSafe(_conn, sql, BuildParams(null, title, description, imageUrl,
            linkUrl, showOnHomepage, startDate, endDate, sortOrder, active));
    }

    public int Update(int id, string title, string description, string imageUrl, string linkUrl,
        bool showOnHomepage, DateTime? startDate, DateTime? endDate, int sortOrder, bool active)
    {
        // ถ้าไม่ได้อัปโหลดรูปใหม่ (imageUrl == null) จะไม่แตะคอลัมน์ Image_Url
        string imageSet = imageUrl != null ? "Image_Url = @ImageUrl," : "";
        string sql = $@"
            UPDATE Promotions SET
                Title = @Title,
                Description = @Description,
                {imageSet}
                Link_Url = @LinkUrl,
                Show_On_Homepage = @ShowHome,
                Start_Date = @StartDate,
                End_Date = @EndDate,
                Sort_Order = @SortOrder,
                Status = @Status,
                Updated_Date = GETDATE()
            WHERE ID = @ID";
        return _code.DatabaseInsertSafe(_conn, sql, BuildParams(id, title, description, imageUrl,
            linkUrl, showOnHomepage, startDate, endDate, sortOrder, active));
    }

    public int Delete(int id)
    {
        return _code.DatabaseInsertSafe(_conn,
            "DELETE FROM Promotions WHERE ID = @ID",
            new Dictionary<string, object> { { "@ID", id } });
    }

    /// <summary>สลับสถานะเปิด/ปิดใช้งาน</summary>
    public int ToggleStatus(int id)
    {
        return _code.DatabaseInsertSafe(_conn,
            "UPDATE Promotions SET Status = CASE WHEN Status = 'True' THEN 'False' ELSE 'True' END, Updated_Date = GETDATE() WHERE ID = @ID",
            new Dictionary<string, object> { { "@ID", id } });
    }

    private static Dictionary<string, object> BuildParams(int? id, string title, string description,
        string imageUrl, string linkUrl, bool showOnHomepage, DateTime? startDate, DateTime? endDate,
        int sortOrder, bool active)
    {
        var p = new Dictionary<string, object>
        {
            { "@Title", title ?? "" },
            { "@Description", (object)description ?? DBNull.Value },
            { "@LinkUrl", (object)linkUrl ?? DBNull.Value },
            { "@ShowHome", showOnHomepage },
            { "@StartDate", startDate.HasValue ? (object)startDate.Value : DBNull.Value },
            { "@EndDate", endDate.HasValue ? (object)endDate.Value : DBNull.Value },
            { "@SortOrder", sortOrder },
            { "@Status", active ? "True" : "False" }
        };
        if (id.HasValue) p["@ID"] = id.Value;
        if (imageUrl != null) p["@ImageUrl"] = imageUrl;
        return p;
    }
}
