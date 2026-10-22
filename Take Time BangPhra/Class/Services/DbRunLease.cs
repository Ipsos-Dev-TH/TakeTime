using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// ล็อกงานเบื้องหลังข้าม process แบบ "สัญญาเช่ามีวันหมดอายุ" (lease) — แถวหนึ่งในตาราง App_Run_Lease
    ///
    /// ทำไมไม่ใช้ sp_getapplock (@LockOwner='Session') อย่างที่เคยทำ:
    ///   ล็อกแบบนั้นผูกกับ "SQL session" ไม่ใช่กับโค้ดเรา และ SqlConnection ของ .NET เป็น
    ///   connection แบบ pool — Dispose() ไม่ได้ปิด socket จริง แค่คืนตัวเชื่อมต่อเข้า pool
    ///   ⇒ ล็อกยังค้างอยู่ที่ฝั่ง SQL จนกว่า connection นั้นจะถูกหยิบมาใช้ใหม่ (sp_reset_connection)
    ///   หรือถูกปิดจริง. เมื่อ deploy DLL ใหม่ AppDomain เก่าถูกทิ้งแต่ w3wp ยังอยู่ →
    ///   pool เก่า (พร้อม connection ที่ถือล็อก) ลอยค้าง ไม่มีใครหยิบไปใช้อีกเลย
    ///   ⇒ ล็อกค้างถาวร งานเบื้องหลัง "ข้ามรอบ" ทุกรอบเงียบ ๆ จนกว่าจะ recycle app pool
    ///   (เกิดจริง: การอ่านอีเมลจอง STAAH หยุดไป 2 วันเต็มโดยไม่มีใครรู้ 19–21 ส.ค. 2026)
    ///
    /// lease นี้ไม่พึ่งอายุของ connection เลย: มีวันหมดอายุอยู่ในข้อมูล ⇒ กรณีเลวร้ายที่สุด
    /// (process ตายคาระหว่างทำงาน / thread ค้าง) งานจะหยุดแค่ไม่เกินอายุ lease แล้วเดินต่อเอง
    ///
    /// ถ้าตารางยังไม่ถูกสร้าง (ยังไม่ได้รันไมเกรชัน) จะคืน lease แบบ "degraded" = ทำงานต่อโดยไม่ล็อก
    /// — ดีกว่าหยุดทั้งระบบ (งานที่ใช้ตัวนี้มี dedup ของตัวเองอยู่แล้ว)
    /// </summary>
    internal sealed class DbRunLease : IDisposable
    {
        private readonly string _conn;
        private readonly string _name;
        private readonly int _minutes;
        private bool _released;

        /// <summary>ตัวระบุเจ้าของ lease รอบนี้ — ต่ออายุ/ปล่อยได้เฉพาะเจ้าของเท่านั้น</summary>
        public string Owner { get; private set; }

        /// <summary>true = ไม่ได้ล็อกจริง (ไม่มีตาราง/DB มีปัญหา) แต่อนุญาตให้ทำงานต่อ</summary>
        public bool Degraded { get; private set; }

        private DbRunLease(string conn, string name, string owner, int minutes, bool degraded)
        {
            _conn = conn; _name = name; Owner = owner; _minutes = minutes; Degraded = degraded;
        }

        private static string BuildOwner()
        {
            string site = "";
            try { site = System.Web.Hosting.HostingEnvironment.SiteName ?? ""; }
            catch { }
            string who = Environment.MachineName + (string.IsNullOrEmpty(site) ? "" : "/" + site);
            string pid = "";
            try { pid = System.Diagnostics.Process.GetCurrentProcess().Id.ToString(); }
            catch { }
            string tag = who + " pid:" + pid + " " + Guid.NewGuid().ToString("N").Substring(0, 8);
            return tag.Length > 200 ? tag.Substring(0, 200) : tag;
        }

        /// <summary>
        /// ขอ lease แบบไม่รอ. คืน object = ได้สิทธิ์ทำงาน (หรือ Degraded), คืน null = มีคนถืออยู่จริง
        /// </summary>
        /// <param name="heldBy">เมื่อคืน null: ใครถืออยู่และหมดอายุเมื่อไหร่ (ไว้เขียน log/แจ้งเตือน)</param>
        public static DbRunLease TryAcquire(string connectionString, string name, int leaseMinutes, out string heldBy)
        {
            heldBy = null;
            if (leaseMinutes < 1) leaseMinutes = 1;
            string owner = BuildOwner();

            // ⚠ @got ต้องประกาศไว้ "ก่อน" UPDATE และรับค่า @@ROWCOUNT ในบรรทัดถัดจาก UPDATE ทันที
            //   โดยไม่มีคำสั่งใดคั่น — คำสั่งที่คั่น (รวมถึง DECLARE) ทำให้ @@ROWCOUNT เพี้ยนได้
            //   ถ้า @got กลายเป็น 0 เสมอ = ขอ lease ไม่ผ่านตลอดกาล = งานเบื้องหลังหยุดถาวร
            //   ซึ่งคือความพังแบบเดียวกับที่ lease ตัวนี้ถูกสร้างมาเพื่อกำจัด
            const string sql = @"
SET NOCOUNT ON;
DECLARE @got INT = -1;

IF OBJECT_ID('App_Run_Lease','U') IS NULL
BEGIN
    SELECT -1 AS Got, CAST(NULL AS NVARCHAR(200)) AS Owner, CAST(NULL AS DATETIME) AS Expires_At;
END
ELSE
BEGIN
    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM App_Run_Lease WHERE Lock_Name = @n)
            INSERT INTO App_Run_Lease (Lock_Name) VALUES (@n);
    END TRY
    BEGIN CATCH
    END CATCH;

    UPDATE App_Run_Lease WITH (UPDLOCK, HOLDLOCK)
       SET Owner = @o, Acquired_At = GETDATE(), Heartbeat_At = GETDATE(),
           Expires_At = DATEADD(MINUTE, @m, GETDATE())
     WHERE Lock_Name = @n
       AND (Expires_At IS NULL OR Expires_At <= GETDATE());
    SET @got = @@ROWCOUNT;

    SELECT @got AS Got, Owner, Expires_At FROM App_Run_Lease WHERE Lock_Name = @n;
END";

            try
            {
                using (var con = new SqlConnection(connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.CommandTimeout = 15;
                        cmd.Parameters.AddWithValue("@n", name);
                        cmd.Parameters.AddWithValue("@o", owner);
                        cmd.Parameters.AddWithValue("@m", leaseMinutes);
                        using (var rd = cmd.ExecuteReader())
                        {
                            if (!rd.Read())
                                return new DbRunLease(connectionString, name, owner, leaseMinutes, true);

                            int got = Convert.ToInt32(rd["Got"]);
                            if (got < 0)
                            {
                                // ยังไม่ได้รันไมเกรชัน — ทำงานต่อโดยไม่ล็อก
                                lock (_failCounts) { _failCounts[name] = 0; }
                                heldBy = "ยังไม่มีตาราง App_Run_Lease (ยังไม่ได้รันไมเกรชัน 33) — ทำงานต่อโดยไม่ล็อก";
                                return new DbRunLease(connectionString, name, owner, leaseMinutes, true);
                            }
                            if (got > 0)
                            {
                                lock (_failCounts) { _failCounts[name] = 0; }
                                return new DbRunLease(connectionString, name, owner, leaseMinutes, false);
                            }

                            lock (_failCounts) { _failCounts[name] = 0; }   // ติดต่อ DB ได้ = ไม่ใช่ความผิดพลาด
                            string holder = rd["Owner"] == DBNull.Value ? "(ไม่ทราบ)" : rd["Owner"].ToString();
                            string until = rd["Expires_At"] == DBNull.Value
                                ? "(ไม่ทราบ)"
                                : Convert.ToDateTime(rd["Expires_At"]).ToString("dd/MM HH:mm:ss");
                            heldBy = holder + " (หมดอายุ " + until + ")";
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // ขอ lease ไม่สำเร็จเพราะ DB (timeout ตอนโหลดสูง / deadlock ชั่วคราว / สิทธิ์)
                //
                // "ทำงานต่อโดยไม่ล็อก" ทันทีนั้นอันตราย: dedup ของงานอ่านอีเมลเป็นแบบ
                // SELECT-แล้ว-INSERT (ไม่ atomic) ⇒ ถ้าอีก worker กำลังทำรอบอยู่จริง
                // จะลงจองซ้ำได้. แต่ "ข้ามตลอดไป" ก็อันตรายเท่ากัน — เป็นเหตุผลเดียวกับที่
                // อีเมลจองหยุด 2 วัน. ทางสายกลาง: ข้ามรอบไปก่อน (ถือว่ามีคนถืออยู่)
                // ถ้าพลาดติดกันหลายรอบ = ไม่ใช่ความบังเอิญชั่วคราว ค่อยยอมทำงานแบบไม่ล็อก
                int fails;
                lock (_failCounts)
                {
                    _failCounts.TryGetValue(name, out fails);
                    fails++;
                    _failCounts[name] = fails;
                }
                if (fails < DegradeAfterConsecutiveFailures)
                {
                    heldBy = $"ขอ lease ไม่สำเร็จ ({ex.Message}) — ข้ามรอบนี้ไว้ก่อน (ครั้งที่ {fails})";
                    return null;
                }
                heldBy = $"ขอ lease ไม่สำเร็จติดกัน {fails} ครั้ง ({ex.Message}) — ทำงานต่อโดยไม่ล็อก";
                return new DbRunLease(connectionString, name, owner, leaseMinutes, true);
            }
        }

        /// <summary>พลาดติดกันกี่ครั้งจึงยอมทำงานแบบไม่ล็อก (กันงานหยุดถาวรเพราะปัญหา DB)</summary>
        private const int DegradeAfterConsecutiveFailures = 3;
        private static readonly Dictionary<string, int> _failCounts = new Dictionary<string, int>();

        /// <summary>ต่ออายุ lease ระหว่างทำงานยาว — เรียกเป็นระยะ กัน lease หมดอายุทั้งที่ยังทำงานอยู่</summary>
        public void Renew()
        {
            if (Degraded || _released) return;
            try
            {
                using (var con = new SqlConnection(_conn))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "UPDATE App_Run_Lease SET Heartbeat_At = GETDATE(), " +
                        "Expires_At = DATEADD(MINUTE, @m, GETDATE()) " +
                        "WHERE Lock_Name = @n AND Owner = @o", con))
                    {
                        cmd.CommandTimeout = 15;
                        cmd.Parameters.AddWithValue("@n", _name);
                        cmd.Parameters.AddWithValue("@o", Owner);
                        cmd.Parameters.AddWithValue("@m", _minutes);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        /// <summary>ปล่อย lease (เฉพาะที่เราถืออยู่) — ปลอดภัยถ้าเรียกซ้ำ</summary>
        public void Dispose()
        {
            if (_released) return;
            _released = true;
            if (Degraded) return;
            try
            {
                using (var con = new SqlConnection(_conn))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "UPDATE App_Run_Lease SET Expires_At = DATEADD(SECOND, -1, GETDATE()), " +
                        "Heartbeat_At = GETDATE() WHERE Lock_Name = @n AND Owner = @o", con))
                    {
                        cmd.CommandTimeout = 15;
                        cmd.Parameters.AddWithValue("@n", _name);
                        cmd.Parameters.AddWithValue("@o", Owner);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }
    }
}
