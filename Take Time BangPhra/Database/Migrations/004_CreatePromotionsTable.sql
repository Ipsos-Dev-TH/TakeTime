-- =============================================
-- TakeTime BangPhra Hotel Management System
-- Migration 004: Promotions / Advertisements
-- Date: 2026-06-09
-- Description: ระบบจัดการโฆษณา/โปรโมชั่น — เก็บรูป + รายละเอียด
--   และตั้งค่าให้เด้งขึ้นหน้าแรก (popup) ตามช่วงเวลาที่กำหนด
-- =============================================

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Promotions' AND xtype='U')
BEGIN
    CREATE TABLE Promotions (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        Image_Url NVARCHAR(500) NULL,
        Link_Url NVARCHAR(500) NULL,          -- ลิงก์เมื่อคลิกที่โปรฯ (ไม่บังคับ)
        Show_On_Homepage BIT NOT NULL DEFAULT 1,  -- เด้ง popup หน้าแรกหรือไม่
        Start_Date DATETIME NULL,             -- เริ่มแสดง (NULL = ไม่จำกัด)
        End_Date DATETIME NULL,               -- สิ้นสุดแสดง (NULL = ไม่จำกัด)
        Sort_Order INT NOT NULL DEFAULT 0,
        Status NVARCHAR(10) NOT NULL DEFAULT 'True', -- 'True' = เปิดใช้งาน
        Created_Date DATETIME NOT NULL DEFAULT GETDATE(),
        Updated_Date DATETIME NULL
    );

    CREATE INDEX IX_Promotions_Active
        ON Promotions (Status, Show_On_Homepage, Sort_Order);
END
GO
