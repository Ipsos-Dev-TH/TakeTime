using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Web;

/// <summary>
/// Result class for operations returning an ID
/// </summary>
public class EmployeeOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public int ID { get; set; }

    public EmployeeOperationResult(bool success, string message, int id)
    {
        Success = success;
        Message = message;
        ID = id;
    }
}

/// <summary>
/// Result class for operations returning a long ID
/// </summary>
public class EmployeeOperationResultLong
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public long ID { get; set; }

    public EmployeeOperationResultLong(bool success, string message, long id)
    {
        Success = success;
        Message = message;
        ID = id;
    }
}

/// <summary>
/// Result class for contract renewal
/// </summary>
public class ContractRenewalResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public int NewContractID { get; set; }
    public string NewContractNumber { get; set; }

    public ContractRenewalResult(bool success, string message, int newContractId, string newContractNumber)
    {
        Success = success;
        Message = message;
        NewContractID = newContractId;
        NewContractNumber = newContractNumber;
    }
}

/// <summary>
/// Employee Service - Business logic for HR Management System
/// Handles employee profiles, documents, service age, contracts, and training
/// </summary>
public class EmployeeService
{
    private readonly string connectionString;

    public EmployeeService()
    {
        connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
    }

    #region Employee Profile Management

    /// <summary>
    /// Get complete employee profile (360° view)
    /// </summary>
    public DataTable GetEmployeeCompleteProfile(short adminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT * FROM vw_Employee_Complete_Profile
                    WHERE Admin_ID = @AdminID";
                cmd.Parameters.AddWithValue("@AdminID", adminId);

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Get all active employees with basic profile info
    /// </summary>
    public DataTable GetAllEmployees()
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        Admin_ID, Name, CurrentPosition, CurrentSalary,
                        Department, MobilePhone, WorkEmail, PhotoPath,
                        TotalServiceYears, TotalServiceMonths, ContractType,
                        ContractDaysUntilExpiry, YearToDateLeaveDays
                    FROM vw_Employee_Complete_Profile
                    ORDER BY Name";

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Create new employee profile
    /// </summary>
    public EmployeeOperationResult CreateEmployeeProfile(
        short adminId, string fullNameTH, DateTime birthDate, string gender,
        string idCardNumber, string mobilePhone, string personalEmail, short updatedByAdminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand("sp_Create_Employee_Profile", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Admin_ID", adminId);
                cmd.Parameters.AddWithValue("@FullNameTH", fullNameTH);
                cmd.Parameters.AddWithValue("@BirthDate", birthDate);
                cmd.Parameters.AddWithValue("@Gender", gender);
                cmd.Parameters.AddWithValue("@IDCardNumber", idCardNumber ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@MobilePhone", mobilePhone ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@PersonalEmail", personalEmail ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@UpdatedBy_AdminID", updatedByAdminId);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string result = reader["Result"].ToString();
                        string message = reader["Message"].ToString();
                        int profileId = result == "Success" ? Convert.ToInt32(reader["ProfileID"]) : 0;
                        return new EmployeeOperationResult(result == "Success", message, profileId);
                    }
                }
            }
        }
        return new EmployeeOperationResult(false, "Unknown error", 0);
    }

    /// <summary>
    /// Update employee profile
    /// </summary>
    public bool UpdateEmployeeProfile(short adminId, Dictionary<string, object> profileData, short updatedByAdminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                var updateFields = new List<string>();
                foreach (var field in profileData)
                {
                    updateFields.Add("[" + field.Key + "] = @" + field.Key);
                    cmd.Parameters.AddWithValue("@" + field.Key, field.Value ?? DBNull.Value);
                }

                cmd.CommandText = @"
                    UPDATE Employee_Profile
                    SET " + string.Join(", ", updateFields) + @",
                        UpdatedDate = GETDATE(),
                        UpdatedBy_AdminID = @UpdatedBy_AdminID
                    WHERE Admin_ID = @Admin_ID";

                cmd.Parameters.AddWithValue("@Admin_ID", adminId);
                cmd.Parameters.AddWithValue("@UpdatedBy_AdminID", updatedByAdminId);

                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }

    #endregion

    #region Service Age Calculation

    /// <summary>
    /// Get employee service age details
    /// </summary>
    public DataTable GetEmployeeServiceAge(short adminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand("sp_Get_Employee_Service_Age", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Admin_ID", adminId);

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Get all employees with service age
    /// </summary>
    public DataTable GetAllEmployeesServiceAge()
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = "SELECT * FROM vw_Employee_Service_Age ORDER BY TotalDays DESC";

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    #endregion

    #region Employment History

    /// <summary>
    /// Record employment event (hire, promotion, salary change, etc.)
    /// </summary>
    public EmployeeOperationResult RecordEmploymentEvent(
        short adminId, string eventType, DateTime eventDate,
        string positionBefore = null, string positionAfter = null,
        decimal? salaryBefore = null, decimal? salaryAfter = null,
        string reason = null, short recordedByAdminId = 0)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand("sp_Record_Employment_Event", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Admin_ID", adminId);
                cmd.Parameters.AddWithValue("@EventType", eventType);
                cmd.Parameters.AddWithValue("@EventDate", eventDate);
                cmd.Parameters.AddWithValue("@PositionBefore", positionBefore ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@PositionAfter", positionAfter ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@SalaryBefore", salaryBefore ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@SalaryAfter", salaryAfter ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Reason", reason ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@RecordedBy_AdminID", recordedByAdminId);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string result = reader["Result"].ToString();
                        string message = reader["Message"].ToString();
                        int eventId = result == "Success" ? Convert.ToInt32(reader["EventID"]) : 0;
                        return new EmployeeOperationResult(result == "Success", message, eventId);
                    }
                }
            }
        }
        return new EmployeeOperationResult(false, "Unknown error", 0);
    }

    /// <summary>
    /// Get employment history for an employee
    /// </summary>
    public DataTable GetEmploymentHistory(short adminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        ID, EventType, EventDate, PositionBefore, PositionAfter,
                        SalaryBefore, SalaryAfter, Reason, Notes, RecordedDate
                    FROM Employee_Employment_History
                    WHERE Admin_ID = @AdminID
                    ORDER BY EventDate DESC, RecordedDate DESC";
                cmd.Parameters.AddWithValue("@AdminID", adminId);

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    #endregion

    #region Document Management

    /// <summary>
    /// Upload employee document
    /// </summary>
    public EmployeeOperationResultLong UploadDocument(
        short adminId, string documentType, string documentName,
        string filePath, long fileSize, string fileType,
        DateTime? expiryDate = null, bool isConfidential = false,
        short uploadedByAdminId = 0)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand("sp_Upload_Employee_Document", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Admin_ID", adminId);
                cmd.Parameters.AddWithValue("@DocumentType", documentType);
                cmd.Parameters.AddWithValue("@DocumentName", documentName);
                cmd.Parameters.AddWithValue("@FilePath", filePath);
                cmd.Parameters.AddWithValue("@FileSize", fileSize);
                cmd.Parameters.AddWithValue("@FileType", fileType);
                cmd.Parameters.AddWithValue("@ExpiryDate", expiryDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@IsConfidential", isConfidential);
                cmd.Parameters.AddWithValue("@UploadedBy_AdminID", uploadedByAdminId);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string result = reader["Result"].ToString();
                        string message = reader["Message"].ToString();
                        long docId = result == "Success" ? Convert.ToInt64(reader["DocumentID"]) : 0;
                        return new EmployeeOperationResultLong(result == "Success", message, docId);
                    }
                }
            }
        }
        return new EmployeeOperationResultLong(false, "Unknown error", 0);
    }

    /// <summary>
    /// Get employee documents
    /// </summary>
    public DataTable GetEmployeeDocuments(short adminId, bool activeOnly = true)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        ID, DocumentType, DocumentName, DocumentNumber,
                        FilePath, FileSize, FileType, IssueDate, ExpiryDate,
                        DaysUntilExpiry, IsConfidential, ApprovalStatus,
                        UploadedDate, Notes
                    FROM Employee_Documents
                    WHERE Admin_ID = @AdminID" +
                    (activeOnly ? " AND IsActive = 1" : "") + @"
                    ORDER BY UploadedDate DESC";
                cmd.Parameters.AddWithValue("@AdminID", adminId);

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Get documents expiry alerts
    /// </summary>
    public DataTable GetDocumentsExpiryAlerts(int daysThreshold = 30)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT * FROM vw_Employee_Documents_Expiry_Alert
                    ORDER BY DaysUntilExpiry ASC";

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Delete employee document
    /// </summary>
    public bool DeleteDocument(long documentId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    UPDATE Employee_Documents
                    SET IsActive = 0, UpdatedDate = GETDATE()
                    WHERE ID = @DocumentID";
                cmd.Parameters.AddWithValue("@DocumentID", documentId);

                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }

    #endregion

    #region Contracts Management

    /// <summary>
    /// Get employee contracts
    /// </summary>
    public DataTable GetEmployeeContracts(short adminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        ID, ContractNumber, ContractType, ContractStatus,
                        StartDate, EndDate, Position, Department, Salary,
                        DaysUntilExpiry, IsRenewed, ApprovedDate
                    FROM Employee_Contracts
                    WHERE Admin_ID = @AdminID
                    ORDER BY StartDate DESC";
                cmd.Parameters.AddWithValue("@AdminID", adminId);

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Get contracts expiry alerts
    /// </summary>
    public DataTable GetContractsExpiryAlerts(int daysThreshold = 90)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT * FROM vw_Employee_Contracts_Expiry_Alert
                    ORDER BY DaysUntilExpiry ASC";

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Generate contract renewal
    /// </summary>
    public ContractRenewalResult RenewContract(
        int oldContractId, DateTime newStartDate, DateTime newEndDate,
        decimal newSalary, short createdByAdminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand("sp_Generate_Contract_Renewal", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Old_Contract_ID", oldContractId);
                cmd.Parameters.AddWithValue("@NewStartDate", newStartDate);
                cmd.Parameters.AddWithValue("@NewEndDate", newEndDate);
                cmd.Parameters.AddWithValue("@NewSalary", newSalary);
                cmd.Parameters.AddWithValue("@CreatedBy_AdminID", createdByAdminId);

                SqlParameter outputParam = new SqlParameter("@New_Contract_ID", SqlDbType.Int)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(outputParam);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string result = reader["Result"].ToString();
                        string message = reader["Message"] != null ? reader["Message"].ToString() : "";
                        int newContractId = result == "Success" ? Convert.ToInt32(reader["NewContractID"]) : 0;
                        string newContractNumber = result == "Success" && reader["NewContractNumber"] != null ? reader["NewContractNumber"].ToString() : "";
                        return new ContractRenewalResult(result == "Success", message, newContractId, newContractNumber);
                    }
                }
            }
        }
        return new ContractRenewalResult(false, "Unknown error", 0, "");
    }

    #endregion

    #region Training Management

    /// <summary>
    /// Get employee training summary
    /// </summary>
    public DataTable GetEmployeeTrainingSummary(short adminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT * FROM vw_Employee_Training_Summary
                    WHERE Admin_ID = @AdminID";
                cmd.Parameters.AddWithValue("@AdminID", adminId);

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Get employee training records
    /// </summary>
    public DataTable GetEmployeeTrainingRecords(short adminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        ID, TrainingType, TrainingName, TrainingProvider,
                        StartDate, EndDate, TotalHours, CompletionStatus,
                        Score, Grade, CertificateNumber, CertificatePath,
                        TrainingCost, IsPaidByCompany
                    FROM Employee_Training
                    WHERE Admin_ID = @AdminID
                    ORDER BY StartDate DESC";
                cmd.Parameters.AddWithValue("@AdminID", adminId);

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    #endregion

    #region Emergency Contacts & Family

    /// <summary>
    /// Get emergency contacts
    /// </summary>
    public DataTable GetEmergencyContacts(short adminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        ID, ContactName, Relationship, MobilePhone, HomePhone,
                        Email, Address, Priority, IsPrimaryContact
                    FROM Employee_Emergency_Contacts
                    WHERE Admin_ID = @AdminID AND IsActive = 1
                    ORDER BY Priority, IsPrimaryContact DESC";
                cmd.Parameters.AddWithValue("@AdminID", adminId);

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Get family information
    /// </summary>
    public DataTable GetFamilyInformation(short adminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        ID, RelationshipType, FullName, BirthDate, Age,
                        Occupation, PhoneNumber, IsDependent, IsBeneficiary,
                        BeneficiaryPercentage
                    FROM Employee_Family
                    WHERE Admin_ID = @AdminID AND IsActive = 1
                    ORDER BY RelationshipType, FullName";
                cmd.Parameters.AddWithValue("@AdminID", adminId);

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    #endregion

    #region Dashboard & Statistics

    /// <summary>
    /// Get HR dashboard statistics
    /// </summary>
    public DataTable GetHRDashboardStats()
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            // Use direct SQL instead of stored procedure
            string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM Admin WHERE Status = 1) AS TotalActiveEmployees,
                    0 AS NewEmployeesThisMonth,
                    0 AS ContractsExpiringWithin30Days,
                    0 AS DocumentsExpiringWithin30Days";

            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.CommandType = CommandType.Text;

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Search employees by various criteria
    /// </summary>
    public DataTable SearchEmployees(string searchTerm = "", string position = "", string department = "")
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                // Use direct query to Admin table with LEFT JOIN to Employee_Salary for Position
                // Admin table columns: ID, Username, Password, FirstName, LastName, NickName, Role, Status
                cmd.CommandText = @"
                    SELECT
                        A.ID AS Admin_ID,
                        ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS Name,
                        ISNULL(ES.Position, A.Role) AS CurrentPosition,
                        A.Role AS Department,
                        A.Username AS MobilePhone,
                        A.Username AS WorkEmail,
                        ES.MonthlySalary AS CurrentSalary,
                        NULL AS PhotoPath,
                        0 AS TotalServiceYears,
                        0 AS TotalServiceMonths,
                        NULL AS ContractDaysUntilExpiry
                    FROM Admin A
                    LEFT JOIN Employee_Salary ES ON ES.Admin_ID = A.ID AND ES.IsActive = 1
                    WHERE A.Status = 1
                      AND (@SearchTerm = '' OR A.FirstName LIKE '%' + @SearchTerm + '%'
                           OR A.LastName LIKE '%' + @SearchTerm + '%'
                           OR A.Username LIKE '%' + @SearchTerm + '%'
                           OR A.NickName LIKE '%' + @SearchTerm + '%')
                      AND (@Position = '' OR ES.Position LIKE '%' + @Position + '%' OR A.Role LIKE '%' + @Position + '%')
                      AND (@Department = '' OR A.Role LIKE '%' + @Department + '%')
                    ORDER BY A.FirstName, A.LastName";

                cmd.Parameters.AddWithValue("@SearchTerm", searchTerm ?? "");
                cmd.Parameters.AddWithValue("@Position", position ?? "");
                cmd.Parameters.AddWithValue("@Department", department ?? "");

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    #endregion
}
