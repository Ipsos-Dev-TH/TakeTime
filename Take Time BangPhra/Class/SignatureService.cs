using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.IO;
using System.Web;

/// <summary>
/// Signature Service - Centralized management of employee signatures
/// Handles upload, retrieval, and PDF integration
/// </summary>
public class SignatureService
{
    private readonly string connectionString;
    private readonly string signatureFolderPath;

    public SignatureService()
    {
        connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        signatureFolderPath = ConfigurationManager.AppSettings["StaffSignatureFolderPath"] ?? "~/Documents/Staff/Signature";
    }

    #region Get Signature Path

    /// <summary>
    /// Get signature path for an admin/employee by ID
    /// Returns the stored path if exists, otherwise falls back to name-based path
    /// </summary>
    public string GetSignaturePath(short adminId)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        SELECT SignaturePath, FirstName, LastName
                        FROM Admin
                        WHERE ID = @AdminID";
                    cmd.Parameters.AddWithValue("@AdminID", adminId);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // If SignaturePath is stored in database, use it
                            string storedPath = reader["SignaturePath"] != DBNull.Value
                                ? reader["SignaturePath"].ToString()
                                : null;

                            if (!string.IsNullOrEmpty(storedPath))
                            {
                                return storedPath;
                            }

                            // Fallback to name-based path for backward compatibility
                            string firstName = reader["FirstName"] != DBNull.Value ? reader["FirstName"].ToString() : "";
                            string lastName = reader["LastName"] != DBNull.Value ? reader["LastName"].ToString() : "";
                            string fullName = (firstName + " " + lastName).Trim().ToLower();

                            if (!string.IsNullOrEmpty(fullName))
                            {
                                // Use virtual path format (not Path.Combine which adds backslashes)
                                string fallbackPath = signatureFolderPath.TrimEnd('/') + "/" + fullName + ".png";
                                string physicalFallback = MapPath(fallbackPath);
                                if (File.Exists(physicalFallback))
                                {
                                    return fallbackPath;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // Silently fail - signature is optional
        }

        return string.Empty;
    }

    /// <summary>
    /// Get signature path for PDF report (returns File:\\ format)
    /// </summary>
    public string GetSignaturePathForPdf(short adminId)
    {
        try
        {
            // First try to get from database SignaturePath column
            string path = GetSignaturePath(adminId);
            if (!string.IsNullOrEmpty(path))
            {
                string mappedPath = MapPath(path);
                if (File.Exists(mappedPath))
                {
                    return "File:\\" + mappedPath;
                }
            }

            // Fallback: Try name-based path (backward compatibility)
            DataRow adminInfo = GetAdminInfo(adminId);
            if (adminInfo != null)
            {
                string fullName = (adminInfo["FirstName"].ToString() + " " + adminInfo["LastName"].ToString()).ToLower();
                string physicalPath = MapPath(signatureFolderPath);
                string signatureFile = Path.Combine(physicalPath, fullName + ".png");

                if (File.Exists(signatureFile))
                {
                    return "File:\\" + signatureFile;
                }
            }
        }
        catch (Exception)
        {
            // Silently fail
        }

        return string.Empty;
    }

    /// <summary>
    /// Get signature path by admin name (for backward compatibility)
    /// </summary>
    public string GetSignaturePathByName(string firstName, string lastName)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        SELECT ID, SignaturePath
                        FROM Admin
                        WHERE FirstName = @FirstName AND LastName = @LastName";
                    cmd.Parameters.AddWithValue("@FirstName", firstName ?? "");
                    cmd.Parameters.AddWithValue("@LastName", lastName ?? "");

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            short adminId = Convert.ToInt16(reader["ID"]);
                            return GetSignaturePath(adminId);
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // Silently fail
        }

        // Fallback to traditional name-based path (use forward slash for virtual paths)
        string fullName = ((firstName ?? "") + " " + (lastName ?? "")).Trim().ToLower();
        return signatureFolderPath.TrimEnd('/') + "/" + fullName + ".png";
    }

    #endregion

    #region Upload/Update Signature

    /// <summary>
    /// Upload or update signature for an admin/employee
    /// </summary>
    public bool UploadSignature(short adminId, HttpPostedFile file, out string message)
    {
        message = "";

        try
        {
            // Validate file
            if (file == null || file.ContentLength == 0)
            {
                message = "No file uploaded";
                return false;
            }

            // Check file type
            string extension = Path.GetExtension(file.FileName).ToLower();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
            {
                message = "Only PNG, JPG, JPEG files are allowed";
                return false;
            }

            // Check file size (max 2MB)
            if (file.ContentLength > 2 * 1024 * 1024)
            {
                message = "File size must not exceed 2MB";
                return false;
            }

            // Create signature folder if not exists
            string physicalPath = MapPath(signatureFolderPath);
            if (!Directory.Exists(physicalPath))
            {
                Directory.CreateDirectory(physicalPath);
            }

            // Generate unique filename
            string fileName = "sig_" + adminId + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".png";
            string filePath = Path.Combine(physicalPath, fileName);
            // Use forward slash for virtual paths (not Path.Combine which adds backslashes)
            string relativePath = signatureFolderPath.TrimEnd('/') + "/" + fileName;

            // Save file
            file.SaveAs(filePath);

            // Update database
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        UPDATE Admin
                        SET SignaturePath = @SignaturePath,
                            UpdatedDate = GETDATE()
                        WHERE ID = @AdminID";
                    cmd.Parameters.AddWithValue("@SignaturePath", relativePath);
                    cmd.Parameters.AddWithValue("@AdminID", adminId);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            message = "Signature uploaded successfully";
            return true;
        }
        catch (Exception ex)
        {
            message = "Error uploading signature: " + ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Delete signature for an admin/employee
    /// </summary>
    public bool DeleteSignature(short adminId, out string message)
    {
        message = "";

        try
        {
            // Get current signature path
            string currentPath = GetSignaturePath(adminId);

            // Delete file if exists
            if (!string.IsNullOrEmpty(currentPath))
            {
                string physicalPath = MapPath(currentPath);
                if (File.Exists(physicalPath))
                {
                    File.Delete(physicalPath);
                }
            }

            // Clear database
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        UPDATE Admin
                        SET SignaturePath = NULL,
                            UpdatedDate = GETDATE()
                        WHERE ID = @AdminID";
                    cmd.Parameters.AddWithValue("@AdminID", adminId);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            message = "Signature deleted successfully";
            return true;
        }
        catch (Exception ex)
        {
            message = "Error deleting signature: " + ex.Message;
            return false;
        }
    }

    #endregion

    #region Signature Data for Reports

    /// <summary>
    /// Get signature DataTable for PDF reports
    /// </summary>
    public DataTable GetSignatureDataForReport(short creatorAdminId, short approverAdminId)
    {
        DataTable dtSignature = new DataTable();
        dtSignature.Columns.Add("AuthorizeName");
        dtSignature.Columns.Add("AuthorizeSignaturePath");
        dtSignature.Columns.Add("CreatedName");
        dtSignature.Columns.Add("CreatedSignaturePath");

        string approverName = "";
        string approverSignature = "";
        string creatorName = "";
        string creatorSignature = "";

        // Get approver info
        DataRow approverInfo = GetAdminInfo(approverAdminId);
        if (approverInfo != null)
        {
            approverName = approverInfo["FirstName"].ToString() + " " + approverInfo["LastName"].ToString();
            approverSignature = GetSignaturePathForPdf(approverAdminId);
        }

        // Get creator info
        DataRow creatorInfo = GetAdminInfo(creatorAdminId);
        if (creatorInfo != null)
        {
            creatorName = creatorInfo["FirstName"].ToString() + " " + creatorInfo["LastName"].ToString();
            creatorSignature = GetSignaturePathForPdf(creatorAdminId);
        }

        dtSignature.Rows.Add(approverName, approverSignature, creatorName, creatorSignature);
        return dtSignature;
    }

    /// <summary>
    /// Get signature DataTable for PDF reports with CEO as approver
    /// </summary>
    public DataTable GetSignatureDataWithCEO(short creatorAdminId)
    {
        DataTable dtSignature = new DataTable();
        dtSignature.Columns.Add("AuthorizeName");
        dtSignature.Columns.Add("AuthorizeSignaturePath");
        dtSignature.Columns.Add("CreatedName");
        dtSignature.Columns.Add("CreatedSignaturePath");

        string approverName = "";
        string approverSignature = "";
        string creatorName = "";
        string creatorSignature = "";

        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Get CEO as approver (matches master: "Select * from Admin Where IsCEO = 'True'")
                using (SqlCommand cmd = new SqlCommand("SELECT FirstName, LastName FROM Admin WHERE IsCEO = 'True'", conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            approverName = reader["FirstName"].ToString() + " " + reader["LastName"].ToString();
                        }
                    }
                }

                // Get creator info
                if (creatorAdminId > 0)
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT FirstName, LastName FROM Admin WHERE ID = @ID", conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", creatorAdminId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                creatorName = reader["FirstName"].ToString() + " " + reader["LastName"].ToString();
                            }
                        }
                    }
                }

                // If creatorAdminId is 0 or creator not found, use CEO for creator
                if (string.IsNullOrEmpty(creatorName))
                {
                    creatorName = approverName;
                }
            }
        }
        catch (Exception)
        {
            // Silently fail
        }

        // Build signature paths - ALWAYS set path like master (no File.Exists check)
        // Master format: "File:\\" + PhysicalPath + "\\" + name.ToLower() + ".png"
        string physicalSignaturePath = MapPath(signatureFolderPath);

        if (!string.IsNullOrEmpty(approverName))
        {
            approverSignature = "File:\\" + physicalSignaturePath + "\\" + approverName.ToLower() + ".png";
        }
        if (!string.IsNullOrEmpty(creatorName))
        {
            creatorSignature = "File:\\" + physicalSignaturePath + "\\" + creatorName.ToLower() + ".png";
        }

        dtSignature.Rows.Add(approverName, approverSignature, creatorName, creatorSignature);
        return dtSignature;
    }

    #endregion

    #region Helper Methods

    private DataRow GetAdminInfo(short adminId)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        SELECT ID, FirstName, LastName, SignaturePath
                        FROM Admin
                        WHERE ID = @AdminID";
                    cmd.Parameters.AddWithValue("@AdminID", adminId);

                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt.Rows.Count > 0 ? dt.Rows[0] : null;
                    }
                }
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    private string MapPath(string virtualPath)
    {
        if (string.IsNullOrEmpty(virtualPath))
        {
            return string.Empty;
        }

        // Check if already a physical path (starts with drive letter like D:\ or C:\)
        if (virtualPath.Length >= 2 && virtualPath[1] == ':')
        {
            return virtualPath;
        }

        // Normalize path - replace backslashes with forward slashes for virtual paths
        // This handles mixed format paths like "~/path\file.png"
        virtualPath = virtualPath.Replace('\\', '/');

        if (HttpContext.Current != null)
        {
            return HttpContext.Current.Server.MapPath(virtualPath);
        }

        // Fallback for non-web context
        if (virtualPath.StartsWith("~"))
        {
            virtualPath = virtualPath.Substring(1);
        }
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, virtualPath.TrimStart('/').Replace('/', '\\'));
    }

    /// <summary>
    /// Check if signature exists for admin
    /// </summary>
    public bool HasSignature(short adminId)
    {
        string path = GetSignaturePath(adminId);
        if (!string.IsNullOrEmpty(path))
        {
            string physicalPath = MapPath(path);
            return File.Exists(physicalPath);
        }
        return false;
    }

    /// <summary>
    /// Get signature URL for web display
    /// </summary>
    public string GetSignatureUrl(short adminId)
    {
        string path = GetSignaturePath(adminId);
        if (!string.IsNullOrEmpty(path))
        {
            // Convert physical path to URL
            if (path.StartsWith("~"))
            {
                return path;
            }
            return "~/" + path.Replace("\\", "/").TrimStart('/');
        }
        return string.Empty;
    }

    /// <summary>
    /// Get signature DataTable for Payment Voucher reports (8 columns)
    /// </summary>
    public DataTable GetPaymentVoucherSignatureData(short creatorAdminId, string receiverIdNumber)
    {
        DataTable dtSignature = new DataTable();
        dtSignature.Columns.Add("CreatedName");
        dtSignature.Columns.Add("Created");
        dtSignature.Columns.Add("ApprovedName");
        dtSignature.Columns.Add("Approved");
        dtSignature.Columns.Add("CheckedName");
        dtSignature.Columns.Add("Checked");
        dtSignature.Columns.Add("ReceivedName");
        dtSignature.Columns.Add("Received");

        string creatorName = "";
        string creatorSignature = "";
        string approverName = "";
        string approverSignature = "";
        string receiverName = "";
        string receiverSignature = "";

        // Get physical signature folder path for PDF (same format as GetSignatureDataWithCEO)
        string physicalSignaturePath = MapPath(signatureFolderPath);

        // Get creator info
        DataRow creatorInfo = GetAdminInfo(creatorAdminId);
        if (creatorInfo != null)
        {
            creatorName = creatorInfo["FirstName"].ToString() + " " + creatorInfo["LastName"].ToString();
            // Build path directly like GetSignatureDataWithCEO (no File.Exists check)
            if (!string.IsNullOrEmpty(creatorName))
            {
                creatorSignature = "File:\\" + physicalSignaturePath + "\\" + creatorName.ToLower() + ".png";
            }
        }

        // Get CEO as approver
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        SELECT TOP 1 ID, FirstName, LastName
                        FROM Admin
                        WHERE IsCEO = 'True' OR IsOwner = 'True'
                        ORDER BY IsCEO DESC";

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            approverName = reader["FirstName"].ToString() + " " + reader["LastName"].ToString();
                            // Build path directly (no File.Exists check)
                            if (!string.IsNullOrEmpty(approverName))
                            {
                                approverSignature = "File:\\" + physicalSignaturePath + "\\" + approverName.ToLower() + ".png";
                            }
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // Silently fail
        }

        // Get receiver info by ID Number
        if (!string.IsNullOrEmpty(receiverIdNumber))
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = @"
                            SELECT ID, FirstName, LastName
                            FROM Admin
                            WHERE IDCard = @IDCard";
                        cmd.Parameters.AddWithValue("@IDCard", receiverIdNumber);

                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                receiverName = reader["FirstName"].ToString() + " " + reader["LastName"].ToString();
                                // Build path directly (no File.Exists check)
                                if (!string.IsNullOrEmpty(receiverName))
                                {
                                    receiverSignature = "File:\\" + physicalSignaturePath + "\\" + receiverName.ToLower() + ".png";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail
            }
        }

        dtSignature.Rows.Add(creatorName, creatorSignature, approverName, approverSignature, "", "", receiverName, receiverSignature);
        return dtSignature;
    }

    #endregion
}
