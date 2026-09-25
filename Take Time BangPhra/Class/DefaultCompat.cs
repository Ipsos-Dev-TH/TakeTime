using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// ตัวช่วยฐานข้อมูลที่ใช้ร่วมกันในชื่อ <c>_Default</c> — ไฟล์นี้มีไว้เพื่อความเข้ากันได้
    /// กับโค้ดเดิมกว่า 19 ไฟล์ที่เขียนว่า <c>_Default code = new _Default();</c>
    /// แล้วเรียก <c>code.DatabaseQuery(...)</c> / <c>DatabaseQuerySafe(...)</c> ฯลฯ
    ///
    /// ประวัติ: เดิม class นี้อยู่ใน code-behind ของหน้า <c>Default_Backup.aspx</c> (หน้าสำรอง
    /// ที่ไม่ได้ใช้แล้ว — หน้าแรกจริงใช้ <c>_Default2</c>) แต่หน้านั้นมีคำสั่ง SQL ที่ต่อสตริง
    /// จากค่าที่รับมาทางเว็บ (SQL injection) จึงถูกลบทิ้ง — ทว่า class ตัวช่วยนี้ยังถูกใช้อยู่
    /// จึงย้ายเฉพาะส่วนที่ปลอดภัยและมีการเรียกใช้จริงมาไว้ที่นี่ ส่วนโค้ดของหน้า
    /// (Page_Load, Calendar, GridView, ดึงรีวิว Google) ไม่ได้ย้ายมาด้วย เพราะเลิกใช้แล้ว
    ///
    /// เมธอดทั้งหมดเป็นตัวส่งต่อไปยัง <see cref="code"/> ซึ่งเป็นตัวจริง — แนะนำให้โค้ดใหม่
    /// ใช้ <c>code</c> โดยตรง และทยอยเลิกใช้ <c>_Default</c>
    /// </summary>
    public partial class _Default : Page
    {
        private readonly code _helper = new code();

        /// <summary>
        /// รันคำสั่ง SQL ดิบ (ไม่มีพารามิเตอร์) — คงไว้เพราะโค้ดเดิมเรียกใช้อยู่
        /// ⚠️ อย่าใช้กับค่าที่มาจากผู้ใช้ ให้ใช้ <see cref="DatabaseInsertSafe"/> แทน
        /// </summary>
        public int DatabaseInsert(string connectionString, string query)
        {
            int insertedId = 0;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (query.Trim().ToUpper().EndsWith("SELECT SCOPE_IDENTITY();"))
                    {
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            insertedId = Convert.ToInt32(result);
                    }
                    else
                    {
                        insertedId = cmd.ExecuteNonQuery();
                    }
                }
            }
            return insertedId;
        }

        /// <summary>
        /// อ่านข้อมูลด้วยคำสั่ง SQL ดิบ — คงไว้เพราะโค้ดเดิมเรียกใช้อยู่
        /// ⚠️ อย่าใช้กับค่าที่มาจากผู้ใช้ ให้ใช้ <see cref="DatabaseQuerySafe"/> แทน
        /// </summary>
        public DataTable DatabaseQuery(string connStr, string cmd)
        {
            DataTable dt = new DataTable();
            using (SqlConnection con = new SqlConnection(connStr))
            {
                try
                {
                    cmd = cmd.Replace("&amp;", "&").Replace("&#39;", "''").Replace("&nbsp;", "");
                    var adapter = new SqlDataAdapter(cmd, con);
                    adapter.Fill(dt);
                }
                catch (Exception ex)
                {
                    // พฤติกรรมเดิมคือกลืน exception แล้วคืนตารางว่าง — คงไว้ไม่ให้หน้าเดิมพัง
                    // แต่บันทึก trace ไว้ให้ไล่ปัญหาได้ (เดิมเงียบสนิท)
                    System.Diagnostics.Trace.TraceError("_Default.DatabaseQuery: " + ex.Message);
                }
            }
            return dt;
        }

        /// <summary>อ่านข้อมูลแบบมีพารามิเตอร์ (ปลอดภัยจาก SQL injection)</summary>
        public DataTable DatabaseQuerySafe(string connStr, string query, Dictionary<string, object> parameters = null)
            => _helper.DatabaseQuerySafe(connStr, query, parameters);

        /// <summary>INSERT/UPDATE/DELETE แบบมีพารามิเตอร์ — คืนจำนวนแถวที่กระทบ</summary>
        public int DatabaseInsertSafe(string connStr, string query, Dictionary<string, object> parameters = null)
            => _helper.DatabaseInsertSafe(connStr, query, parameters);

        /// <summary>INSERT แบบมีพารามิเตอร์ — คืน ID ใหม่ (ต้องมี SCOPE_IDENTITY ในคำสั่ง)</summary>
        public int DatabaseInsertReturnSafe(string connStr, string query, Dictionary<string, object> parameters = null)
            => _helper.DatabaseInsertReturnSafe(connStr, query, parameters);

        /// <summary>เพิ่ม/อัปเดตข้อมูลลูกค้า (ส่งต่อไปยัง code.UpsertCustomer)</summary>
        public long UpsertCustomer(
            string connStr, string mobilePhone, string name, string nickName, string comeFrom,
            string remark, string fullName, string address, string idNumber, string email,
            int customerTypeID, int addressID, string address1, string branchNumber)
            => _helper.UpsertCustomer(connStr, mobilePhone, name, nickName, comeFrom, remark,
                fullName, address, idNumber, email, customerTypeID, addressID, address1, branchNumber);
    }
}
