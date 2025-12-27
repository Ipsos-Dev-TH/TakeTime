using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Take_Time_BangPhra.Class.Services
{
    /// <summary>
    /// Service for managing maintenance requests and preventive maintenance
    /// </summary>
    public class MaintenanceRequestService
    {
        private readonly string _connectionString;

        public MaintenanceRequestService(string connectionString)
        {
            _connectionString = connectionString;
        }

        #region Request Management

        /// <summary>
        /// Create a new maintenance request
        /// </summary>
        public int CreateMaintenanceRequest(MaintenanceRequest request)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"INSERT INTO MaintenanceRequests
                    (RequestNumber, LocationType, LocationId, LocationName, Category, Priority,
                     Title, Description, ReportedBy, ReportedByType, RequestDate, Status,
                     EstimatedCost, Notes, CreatedAt)
                    VALUES
                    (@RequestNumber, @LocationType, @LocationId, @LocationName, @Category, @Priority,
                     @Title, @Description, @ReportedBy, @ReportedByType, @RequestDate, @Status,
                     @EstimatedCost, @Notes, GETDATE());
                    SELECT SCOPE_IDENTITY();";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@RequestNumber", GenerateRequestNumber());
                    cmd.Parameters.AddWithValue("@LocationType", request.LocationType);
                    cmd.Parameters.AddWithValue("@LocationId", request.LocationId);
                    cmd.Parameters.AddWithValue("@LocationName", request.LocationName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Category", request.Category);
                    cmd.Parameters.AddWithValue("@Priority", request.Priority);
                    cmd.Parameters.AddWithValue("@Title", request.Title);
                    cmd.Parameters.AddWithValue("@Description", request.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ReportedBy", request.ReportedBy);
                    cmd.Parameters.AddWithValue("@ReportedByType", request.ReportedByType);
                    cmd.Parameters.AddWithValue("@RequestDate", DateTime.Now);
                    cmd.Parameters.AddWithValue("@Status", "PENDING");
                    cmd.Parameters.AddWithValue("@EstimatedCost", request.EstimatedCost);
                    cmd.Parameters.AddWithValue("@Notes", request.Notes ?? (object)DBNull.Value);

                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        /// <summary>
        /// Generate unique request number
        /// </summary>
        private string GenerateRequestNumber()
        {
            return $"MR-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";
        }

        /// <summary>
        /// Get maintenance request by ID
        /// </summary>
        public MaintenanceRequest GetRequestById(int requestId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"SELECT * FROM MaintenanceRequests WHERE RequestId = @RequestId";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@RequestId", requestId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return MapRequestFromReader(reader);
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Get all maintenance requests with filters
        /// </summary>
        public List<MaintenanceRequest> GetRequests(string status = null, string priority = null,
            string category = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            List<MaintenanceRequest> requests = new List<MaintenanceRequest>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"SELECT * FROM MaintenanceRequests WHERE 1=1";

                if (!string.IsNullOrEmpty(status))
                    sql += " AND Status = @Status";
                if (!string.IsNullOrEmpty(priority))
                    sql += " AND Priority = @Priority";
                if (!string.IsNullOrEmpty(category))
                    sql += " AND Category = @Category";
                if (fromDate.HasValue)
                    sql += " AND RequestDate >= @FromDate";
                if (toDate.HasValue)
                    sql += " AND RequestDate <= @ToDate";

                sql += " ORDER BY CASE Priority WHEN 'EMERGENCY' THEN 1 WHEN 'HIGH' THEN 2 WHEN 'MEDIUM' THEN 3 ELSE 4 END, RequestDate DESC";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    if (!string.IsNullOrEmpty(status))
                        cmd.Parameters.AddWithValue("@Status", status);
                    if (!string.IsNullOrEmpty(priority))
                        cmd.Parameters.AddWithValue("@Priority", priority);
                    if (!string.IsNullOrEmpty(category))
                        cmd.Parameters.AddWithValue("@Category", category);
                    if (fromDate.HasValue)
                        cmd.Parameters.AddWithValue("@FromDate", fromDate.Value);
                    if (toDate.HasValue)
                        cmd.Parameters.AddWithValue("@ToDate", toDate.Value);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            requests.Add(MapRequestFromReader(reader));
                        }
                    }
                }
            }
            return requests;
        }

        /// <summary>
        /// Get requests for a specific location (room, common area, etc.)
        /// </summary>
        public List<MaintenanceRequest> GetRequestsByLocation(string locationType, int locationId)
        {
            List<MaintenanceRequest> requests = new List<MaintenanceRequest>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"SELECT * FROM MaintenanceRequests
                    WHERE LocationType = @LocationType AND LocationId = @LocationId
                    ORDER BY RequestDate DESC";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@LocationType", locationType);
                    cmd.Parameters.AddWithValue("@LocationId", locationId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            requests.Add(MapRequestFromReader(reader));
                        }
                    }
                }
            }
            return requests;
        }

        private MaintenanceRequest MapRequestFromReader(SqlDataReader reader)
        {
            return new MaintenanceRequest
            {
                RequestId = Convert.ToInt32(reader["RequestId"]),
                RequestNumber = reader["RequestNumber"].ToString(),
                LocationType = reader["LocationType"].ToString(),
                LocationId = Convert.ToInt32(reader["LocationId"]),
                LocationName = reader["LocationName"]?.ToString(),
                Category = reader["Category"].ToString(),
                Priority = reader["Priority"].ToString(),
                Title = reader["Title"].ToString(),
                Description = reader["Description"]?.ToString(),
                ReportedBy = Convert.ToInt32(reader["ReportedBy"]),
                ReportedByType = reader["ReportedByType"].ToString(),
                RequestDate = Convert.ToDateTime(reader["RequestDate"]),
                Status = reader["Status"].ToString(),
                AssignedTo = reader["AssignedTo"] != DBNull.Value ? Convert.ToInt32(reader["AssignedTo"]) : (int?)null,
                AssignedDate = reader["AssignedDate"] != DBNull.Value ? Convert.ToDateTime(reader["AssignedDate"]) : (DateTime?)null,
                StartedDate = reader["StartedDate"] != DBNull.Value ? Convert.ToDateTime(reader["StartedDate"]) : (DateTime?)null,
                CompletedDate = reader["CompletedDate"] != DBNull.Value ? Convert.ToDateTime(reader["CompletedDate"]) : (DateTime?)null,
                EstimatedCost = reader["EstimatedCost"] != DBNull.Value ? Convert.ToDecimal(reader["EstimatedCost"]) : 0,
                ActualCost = reader["ActualCost"] != DBNull.Value ? Convert.ToDecimal(reader["ActualCost"]) : 0,
                Notes = reader["Notes"]?.ToString()
            };
        }

        #endregion

        #region Status Management

        /// <summary>
        /// Assign technician to a request
        /// </summary>
        public bool AssignTechnician(int requestId, int technicianId, int assignedBy)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        // Update request
                        string sql = @"UPDATE MaintenanceRequests
                            SET AssignedTo = @TechnicianId, AssignedDate = GETDATE(),
                                Status = 'ASSIGNED', UpdatedAt = GETDATE()
                            WHERE RequestId = @RequestId";

                        using (SqlCommand cmd = new SqlCommand(sql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@RequestId", requestId);
                            cmd.Parameters.AddWithValue("@TechnicianId", technicianId);
                            cmd.ExecuteNonQuery();
                        }

                        // Log status change
                        LogStatusChange(conn, trans, requestId, "PENDING", "ASSIGNED", assignedBy,
                            $"Assigned to technician ID: {technicianId}");

                        trans.Commit();
                        return true;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Start work on a request
        /// </summary>
        public bool StartWork(int requestId, int technicianId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        string prevStatus = GetRequestStatus(conn, trans, requestId);

                        string sql = @"UPDATE MaintenanceRequests
                            SET StartedDate = GETDATE(), Status = 'IN_PROGRESS', UpdatedAt = GETDATE()
                            WHERE RequestId = @RequestId AND AssignedTo = @TechnicianId";

                        using (SqlCommand cmd = new SqlCommand(sql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@RequestId", requestId);
                            cmd.Parameters.AddWithValue("@TechnicianId", technicianId);
                            int rows = cmd.ExecuteNonQuery();

                            if (rows > 0)
                            {
                                LogStatusChange(conn, trans, requestId, prevStatus, "IN_PROGRESS", technicianId, "Work started");
                                trans.Commit();
                                return true;
                            }
                        }

                        trans.Rollback();
                        return false;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Complete a maintenance request
        /// </summary>
        public bool CompleteRequest(int requestId, int technicianId, decimal actualCost, string completionNotes)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        string prevStatus = GetRequestStatus(conn, trans, requestId);

                        string sql = @"UPDATE MaintenanceRequests
                            SET CompletedDate = GETDATE(), Status = 'COMPLETED',
                                ActualCost = @ActualCost, Notes = ISNULL(Notes, '') + CHAR(13) + CHAR(10) + @CompletionNotes,
                                UpdatedAt = GETDATE()
                            WHERE RequestId = @RequestId AND AssignedTo = @TechnicianId";

                        using (SqlCommand cmd = new SqlCommand(sql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@RequestId", requestId);
                            cmd.Parameters.AddWithValue("@TechnicianId", technicianId);
                            cmd.Parameters.AddWithValue("@ActualCost", actualCost);
                            cmd.Parameters.AddWithValue("@CompletionNotes", $"[{DateTime.Now:yyyy-MM-dd HH:mm}] {completionNotes}");

                            int rows = cmd.ExecuteNonQuery();
                            if (rows > 0)
                            {
                                LogStatusChange(conn, trans, requestId, prevStatus, "COMPLETED", technicianId, completionNotes);
                                trans.Commit();
                                return true;
                            }
                        }

                        trans.Rollback();
                        return false;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Put request on hold
        /// </summary>
        public bool PutOnHold(int requestId, int userId, string reason)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        string prevStatus = GetRequestStatus(conn, trans, requestId);

                        string sql = @"UPDATE MaintenanceRequests
                            SET Status = 'ON_HOLD',
                                Notes = ISNULL(Notes, '') + CHAR(13) + CHAR(10) + @HoldNote,
                                UpdatedAt = GETDATE()
                            WHERE RequestId = @RequestId";

                        using (SqlCommand cmd = new SqlCommand(sql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@RequestId", requestId);
                            cmd.Parameters.AddWithValue("@HoldNote", $"[{DateTime.Now:yyyy-MM-dd HH:mm}] ON HOLD: {reason}");
                            cmd.ExecuteNonQuery();
                        }

                        LogStatusChange(conn, trans, requestId, prevStatus, "ON_HOLD", userId, reason);
                        trans.Commit();
                        return true;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Cancel a maintenance request
        /// </summary>
        public bool CancelRequest(int requestId, int userId, string reason)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        string prevStatus = GetRequestStatus(conn, trans, requestId);

                        string sql = @"UPDATE MaintenanceRequests
                            SET Status = 'CANCELLED',
                                Notes = ISNULL(Notes, '') + CHAR(13) + CHAR(10) + @CancelNote,
                                UpdatedAt = GETDATE()
                            WHERE RequestId = @RequestId";

                        using (SqlCommand cmd = new SqlCommand(sql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@RequestId", requestId);
                            cmd.Parameters.AddWithValue("@CancelNote", $"[{DateTime.Now:yyyy-MM-dd HH:mm}] CANCELLED: {reason}");
                            cmd.ExecuteNonQuery();
                        }

                        LogStatusChange(conn, trans, requestId, prevStatus, "CANCELLED", userId, reason);
                        trans.Commit();
                        return true;
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        private string GetRequestStatus(SqlConnection conn, SqlTransaction trans, int requestId)
        {
            string sql = "SELECT Status FROM MaintenanceRequests WHERE RequestId = @RequestId";
            using (SqlCommand cmd = new SqlCommand(sql, conn, trans))
            {
                cmd.Parameters.AddWithValue("@RequestId", requestId);
                return cmd.ExecuteScalar()?.ToString();
            }
        }

        private void LogStatusChange(SqlConnection conn, SqlTransaction trans, int requestId,
            string fromStatus, string toStatus, int changedBy, string notes)
        {
            string sql = @"INSERT INTO MaintenanceStatusLog
                (RequestId, FromStatus, ToStatus, ChangedBy, ChangedAt, Notes)
                VALUES (@RequestId, @FromStatus, @ToStatus, @ChangedBy, GETDATE(), @Notes)";

            using (SqlCommand cmd = new SqlCommand(sql, conn, trans))
            {
                cmd.Parameters.AddWithValue("@RequestId", requestId);
                cmd.Parameters.AddWithValue("@FromStatus", fromStatus ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ToStatus", toStatus);
                cmd.Parameters.AddWithValue("@ChangedBy", changedBy);
                cmd.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region Preventive Maintenance

        /// <summary>
        /// Create a preventive maintenance schedule
        /// </summary>
        public int CreatePreventiveSchedule(PreventiveMaintenanceSchedule schedule)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"INSERT INTO PreventiveMaintenanceSchedules
                    (ScheduleName, LocationType, LocationId, LocationName, Category, Description,
                     FrequencyType, FrequencyValue, LastPerformedDate, NextDueDate,
                     EstimatedDuration, EstimatedCost, AssignedTeam, IsActive, CreatedBy, CreatedAt)
                    VALUES
                    (@ScheduleName, @LocationType, @LocationId, @LocationName, @Category, @Description,
                     @FrequencyType, @FrequencyValue, @LastPerformedDate, @NextDueDate,
                     @EstimatedDuration, @EstimatedCost, @AssignedTeam, 1, @CreatedBy, GETDATE());
                    SELECT SCOPE_IDENTITY();";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ScheduleName", schedule.ScheduleName);
                    cmd.Parameters.AddWithValue("@LocationType", schedule.LocationType);
                    cmd.Parameters.AddWithValue("@LocationId", schedule.LocationId);
                    cmd.Parameters.AddWithValue("@LocationName", schedule.LocationName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Category", schedule.Category);
                    cmd.Parameters.AddWithValue("@Description", schedule.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@FrequencyType", schedule.FrequencyType);
                    cmd.Parameters.AddWithValue("@FrequencyValue", schedule.FrequencyValue);
                    cmd.Parameters.AddWithValue("@LastPerformedDate", schedule.LastPerformedDate ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@NextDueDate", schedule.NextDueDate);
                    cmd.Parameters.AddWithValue("@EstimatedDuration", schedule.EstimatedDurationMinutes);
                    cmd.Parameters.AddWithValue("@EstimatedCost", schedule.EstimatedCost);
                    cmd.Parameters.AddWithValue("@AssignedTeam", schedule.AssignedTeam ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedBy", schedule.CreatedBy);

                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        /// <summary>
        /// Get due preventive maintenance schedules
        /// </summary>
        public List<PreventiveMaintenanceSchedule> GetDueSchedules(int daysAhead = 7)
        {
            List<PreventiveMaintenanceSchedule> schedules = new List<PreventiveMaintenanceSchedule>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"SELECT * FROM PreventiveMaintenanceSchedules
                    WHERE IsActive = 1 AND NextDueDate <= DATEADD(day, @DaysAhead, GETDATE())
                    ORDER BY NextDueDate";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@DaysAhead", daysAhead);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            schedules.Add(MapScheduleFromReader(reader));
                        }
                    }
                }
            }
            return schedules;
        }

        /// <summary>
        /// Generate maintenance requests from due schedules
        /// </summary>
        public int GenerateScheduledMaintenanceRequests()
        {
            int generatedCount = 0;
            var dueSchedules = GetDueSchedules(0); // Get schedules due today or overdue

            foreach (var schedule in dueSchedules)
            {
                // Check if request already exists for this schedule today
                if (!HasPendingRequestForSchedule(schedule.ScheduleId))
                {
                    var request = new MaintenanceRequest
                    {
                        LocationType = schedule.LocationType,
                        LocationId = schedule.LocationId,
                        LocationName = schedule.LocationName,
                        Category = schedule.Category,
                        Priority = "MEDIUM",
                        Title = $"[PM] {schedule.ScheduleName}",
                        Description = schedule.Description,
                        ReportedBy = schedule.CreatedBy,
                        ReportedByType = "SYSTEM",
                        EstimatedCost = schedule.EstimatedCost
                    };

                    int requestId = CreateMaintenanceRequest(request);
                    if (requestId > 0)
                    {
                        // Link request to schedule
                        LinkRequestToSchedule(requestId, schedule.ScheduleId);
                        generatedCount++;
                    }
                }
            }

            return generatedCount;
        }

        private bool HasPendingRequestForSchedule(int scheduleId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"SELECT COUNT(*) FROM MaintenanceRequests mr
                    INNER JOIN PreventiveMaintenanceRequestLinks pl ON mr.RequestId = pl.RequestId
                    WHERE pl.ScheduleId = @ScheduleId AND mr.Status IN ('PENDING', 'ASSIGNED', 'IN_PROGRESS')";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ScheduleId", scheduleId);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        private void LinkRequestToSchedule(int requestId, int scheduleId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"INSERT INTO PreventiveMaintenanceRequestLinks
                    (ScheduleId, RequestId, CreatedAt) VALUES (@ScheduleId, @RequestId, GETDATE())";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ScheduleId", scheduleId);
                    cmd.Parameters.AddWithValue("@RequestId", requestId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Update schedule after completion
        /// </summary>
        public void UpdateScheduleAfterCompletion(int scheduleId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Get schedule details
                var schedule = GetScheduleById(scheduleId);
                if (schedule == null) return;

                // Calculate next due date based on frequency
                DateTime nextDueDate = CalculateNextDueDate(DateTime.Now, schedule.FrequencyType, schedule.FrequencyValue);

                string sql = @"UPDATE PreventiveMaintenanceSchedules
                    SET LastPerformedDate = GETDATE(), NextDueDate = @NextDueDate, UpdatedAt = GETDATE()
                    WHERE ScheduleId = @ScheduleId";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ScheduleId", scheduleId);
                    cmd.Parameters.AddWithValue("@NextDueDate", nextDueDate);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private DateTime CalculateNextDueDate(DateTime fromDate, string frequencyType, int frequencyValue)
        {
            switch (frequencyType.ToUpper())
            {
                case "DAILY":
                    return fromDate.AddDays(frequencyValue);
                case "WEEKLY":
                    return fromDate.AddDays(frequencyValue * 7);
                case "MONTHLY":
                    return fromDate.AddMonths(frequencyValue);
                case "QUARTERLY":
                    return fromDate.AddMonths(frequencyValue * 3);
                case "YEARLY":
                    return fromDate.AddYears(frequencyValue);
                default:
                    return fromDate.AddDays(frequencyValue);
            }
        }

        public PreventiveMaintenanceSchedule GetScheduleById(int scheduleId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT * FROM PreventiveMaintenanceSchedules WHERE ScheduleId = @ScheduleId";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ScheduleId", scheduleId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return MapScheduleFromReader(reader);
                        }
                    }
                }
            }
            return null;
        }

        private PreventiveMaintenanceSchedule MapScheduleFromReader(SqlDataReader reader)
        {
            return new PreventiveMaintenanceSchedule
            {
                ScheduleId = Convert.ToInt32(reader["ScheduleId"]),
                ScheduleName = reader["ScheduleName"].ToString(),
                LocationType = reader["LocationType"].ToString(),
                LocationId = Convert.ToInt32(reader["LocationId"]),
                LocationName = reader["LocationName"]?.ToString(),
                Category = reader["Category"].ToString(),
                Description = reader["Description"]?.ToString(),
                FrequencyType = reader["FrequencyType"].ToString(),
                FrequencyValue = Convert.ToInt32(reader["FrequencyValue"]),
                LastPerformedDate = reader["LastPerformedDate"] != DBNull.Value ? Convert.ToDateTime(reader["LastPerformedDate"]) : (DateTime?)null,
                NextDueDate = Convert.ToDateTime(reader["NextDueDate"]),
                EstimatedDurationMinutes = Convert.ToInt32(reader["EstimatedDuration"]),
                EstimatedCost = Convert.ToDecimal(reader["EstimatedCost"]),
                AssignedTeam = reader["AssignedTeam"]?.ToString(),
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                CreatedBy = Convert.ToInt32(reader["CreatedBy"])
            };
        }

        #endregion

        #region Parts and Inventory

        /// <summary>
        /// Record parts used for a maintenance request
        /// </summary>
        public void RecordPartsUsed(int requestId, List<MaintenancePart> parts)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var part in parts)
                        {
                            // Insert part usage record
                            string sql = @"INSERT INTO MaintenancePartsUsed
                                (RequestId, PartId, PartName, Quantity, UnitCost, TotalCost, UsedAt)
                                VALUES (@RequestId, @PartId, @PartName, @Quantity, @UnitCost, @TotalCost, GETDATE())";

                            using (SqlCommand cmd = new SqlCommand(sql, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@RequestId", requestId);
                                cmd.Parameters.AddWithValue("@PartId", part.PartId);
                                cmd.Parameters.AddWithValue("@PartName", part.PartName);
                                cmd.Parameters.AddWithValue("@Quantity", part.Quantity);
                                cmd.Parameters.AddWithValue("@UnitCost", part.UnitCost);
                                cmd.Parameters.AddWithValue("@TotalCost", part.Quantity * part.UnitCost);
                                cmd.ExecuteNonQuery();
                            }

                            // Update inventory
                            string updateSql = @"UPDATE MaintenancePartsInventory
                                SET CurrentStock = CurrentStock - @Quantity, UpdatedAt = GETDATE()
                                WHERE PartId = @PartId";

                            using (SqlCommand cmd = new SqlCommand(updateSql, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@PartId", part.PartId);
                                cmd.Parameters.AddWithValue("@Quantity", part.Quantity);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        trans.Commit();
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Get low stock parts
        /// </summary>
        public List<MaintenancePart> GetLowStockParts()
        {
            List<MaintenancePart> parts = new List<MaintenancePart>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"SELECT * FROM MaintenancePartsInventory
                    WHERE CurrentStock <= ReorderLevel AND IsActive = 1
                    ORDER BY (CurrentStock * 1.0 / ReorderLevel)";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            parts.Add(new MaintenancePart
                            {
                                PartId = Convert.ToInt32(reader["PartId"]),
                                PartName = reader["PartName"].ToString(),
                                PartNumber = reader["PartNumber"].ToString(),
                                Category = reader["Category"].ToString(),
                                CurrentStock = Convert.ToInt32(reader["CurrentStock"]),
                                ReorderLevel = Convert.ToInt32(reader["ReorderLevel"]),
                                UnitCost = Convert.ToDecimal(reader["UnitCost"])
                            });
                        }
                    }
                }
            }
            return parts;
        }

        #endregion

        #region Analytics and Reports

        /// <summary>
        /// Get maintenance statistics
        /// </summary>
        public MaintenanceStatistics GetStatistics(DateTime fromDate, DateTime toDate)
        {
            var stats = new MaintenanceStatistics();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Total requests
                string sql = @"SELECT
                    COUNT(*) as TotalRequests,
                    SUM(CASE WHEN Status = 'COMPLETED' THEN 1 ELSE 0 END) as CompletedRequests,
                    SUM(CASE WHEN Status = 'PENDING' THEN 1 ELSE 0 END) as PendingRequests,
                    SUM(CASE WHEN Status = 'IN_PROGRESS' THEN 1 ELSE 0 END) as InProgressRequests,
                    SUM(CASE WHEN Priority = 'EMERGENCY' THEN 1 ELSE 0 END) as EmergencyRequests,
                    SUM(ActualCost) as TotalCost,
                    AVG(CASE WHEN CompletedDate IS NOT NULL THEN DATEDIFF(hour, RequestDate, CompletedDate) ELSE NULL END) as AvgResolutionHours
                FROM MaintenanceRequests
                WHERE RequestDate BETWEEN @FromDate AND @ToDate";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            stats.TotalRequests = Convert.ToInt32(reader["TotalRequests"]);
                            stats.CompletedRequests = Convert.ToInt32(reader["CompletedRequests"]);
                            stats.PendingRequests = Convert.ToInt32(reader["PendingRequests"]);
                            stats.InProgressRequests = Convert.ToInt32(reader["InProgressRequests"]);
                            stats.EmergencyRequests = Convert.ToInt32(reader["EmergencyRequests"]);
                            stats.TotalCost = reader["TotalCost"] != DBNull.Value ? Convert.ToDecimal(reader["TotalCost"]) : 0;
                            stats.AverageResolutionHours = reader["AvgResolutionHours"] != DBNull.Value ? Convert.ToDouble(reader["AvgResolutionHours"]) : 0;
                        }
                    }
                }

                // Requests by category
                sql = @"SELECT Category, COUNT(*) as Count
                    FROM MaintenanceRequests
                    WHERE RequestDate BETWEEN @FromDate AND @ToDate
                    GROUP BY Category ORDER BY Count DESC";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        stats.RequestsByCategory = new Dictionary<string, int>();
                        while (reader.Read())
                        {
                            stats.RequestsByCategory[reader["Category"].ToString()] = Convert.ToInt32(reader["Count"]);
                        }
                    }
                }

                // Cost by category
                sql = @"SELECT Category, SUM(ActualCost) as TotalCost
                    FROM MaintenanceRequests
                    WHERE RequestDate BETWEEN @FromDate AND @ToDate AND CompletedDate IS NOT NULL
                    GROUP BY Category ORDER BY TotalCost DESC";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        stats.CostByCategory = new Dictionary<string, decimal>();
                        while (reader.Read())
                        {
                            stats.CostByCategory[reader["Category"].ToString()] = Convert.ToDecimal(reader["TotalCost"]);
                        }
                    }
                }
            }

            return stats;
        }

        /// <summary>
        /// Get technician performance
        /// </summary>
        public List<TechnicianPerformance> GetTechnicianPerformance(DateTime fromDate, DateTime toDate)
        {
            List<TechnicianPerformance> performance = new List<TechnicianPerformance>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"SELECT
                    e.Id as TechnicianId,
                    e.FirstName + ' ' + e.LastName as TechnicianName,
                    COUNT(mr.RequestId) as TotalAssigned,
                    SUM(CASE WHEN mr.Status = 'COMPLETED' THEN 1 ELSE 0 END) as Completed,
                    AVG(CASE WHEN mr.CompletedDate IS NOT NULL
                        THEN DATEDIFF(hour, mr.StartedDate, mr.CompletedDate) ELSE NULL END) as AvgCompletionHours,
                    SUM(mr.ActualCost) as TotalCostHandled
                FROM MaintenanceRequests mr
                INNER JOIN Employees e ON mr.AssignedTo = e.Id
                WHERE mr.AssignedDate BETWEEN @FromDate AND @ToDate
                GROUP BY e.Id, e.FirstName, e.LastName
                ORDER BY Completed DESC";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            performance.Add(new TechnicianPerformance
                            {
                                TechnicianId = Convert.ToInt32(reader["TechnicianId"]),
                                TechnicianName = reader["TechnicianName"].ToString(),
                                TotalAssigned = Convert.ToInt32(reader["TotalAssigned"]),
                                Completed = Convert.ToInt32(reader["Completed"]),
                                AverageCompletionHours = reader["AvgCompletionHours"] != DBNull.Value ? Convert.ToDouble(reader["AvgCompletionHours"]) : 0,
                                TotalCostHandled = reader["TotalCostHandled"] != DBNull.Value ? Convert.ToDecimal(reader["TotalCostHandled"]) : 0
                            });
                        }
                    }
                }
            }

            return performance;
        }

        /// <summary>
        /// Get rooms requiring most maintenance
        /// </summary>
        public List<RoomMaintenanceHistory> GetHighMaintenanceRooms(int topN = 10)
        {
            List<RoomMaintenanceHistory> rooms = new List<RoomMaintenanceHistory>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"SELECT TOP (@TopN)
                    LocationId as RoomId,
                    LocationName as RoomName,
                    COUNT(*) as TotalRequests,
                    SUM(ActualCost) as TotalCost,
                    MAX(RequestDate) as LastRequestDate
                FROM MaintenanceRequests
                WHERE LocationType = 'ROOM'
                GROUP BY LocationId, LocationName
                ORDER BY TotalRequests DESC";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@TopN", topN);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            rooms.Add(new RoomMaintenanceHistory
                            {
                                RoomId = Convert.ToInt32(reader["RoomId"]),
                                RoomName = reader["RoomName"]?.ToString(),
                                TotalRequests = Convert.ToInt32(reader["TotalRequests"]),
                                TotalCost = reader["TotalCost"] != DBNull.Value ? Convert.ToDecimal(reader["TotalCost"]) : 0,
                                LastRequestDate = reader["LastRequestDate"] != DBNull.Value ? Convert.ToDateTime(reader["LastRequestDate"]) : (DateTime?)null
                            });
                        }
                    }
                }
            }

            return rooms;
        }

        #endregion
    }

    #region Data Models

    public class MaintenanceRequest
    {
        public int RequestId { get; set; }
        public string RequestNumber { get; set; }
        public string LocationType { get; set; } // ROOM, COMMON_AREA, FACILITY, EQUIPMENT
        public int LocationId { get; set; }
        public string LocationName { get; set; }
        public string Category { get; set; } // PLUMBING, ELECTRICAL, HVAC, FURNITURE, APPLIANCE, STRUCTURAL, OTHER
        public string Priority { get; set; } // EMERGENCY, HIGH, MEDIUM, LOW
        public string Title { get; set; }
        public string Description { get; set; }
        public int ReportedBy { get; set; }
        public string ReportedByType { get; set; } // GUEST, STAFF, SYSTEM
        public DateTime RequestDate { get; set; }
        public string Status { get; set; } // PENDING, ASSIGNED, IN_PROGRESS, ON_HOLD, COMPLETED, CANCELLED
        public int? AssignedTo { get; set; }
        public DateTime? AssignedDate { get; set; }
        public DateTime? StartedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal ActualCost { get; set; }
        public string Notes { get; set; }
    }

    public class PreventiveMaintenanceSchedule
    {
        public int ScheduleId { get; set; }
        public string ScheduleName { get; set; }
        public string LocationType { get; set; }
        public int LocationId { get; set; }
        public string LocationName { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string FrequencyType { get; set; } // DAILY, WEEKLY, MONTHLY, QUARTERLY, YEARLY
        public int FrequencyValue { get; set; }
        public DateTime? LastPerformedDate { get; set; }
        public DateTime NextDueDate { get; set; }
        public int EstimatedDurationMinutes { get; set; }
        public decimal EstimatedCost { get; set; }
        public string AssignedTeam { get; set; }
        public bool IsActive { get; set; }
        public int CreatedBy { get; set; }
    }

    public class MaintenancePart
    {
        public int PartId { get; set; }
        public string PartNumber { get; set; }
        public string PartName { get; set; }
        public string Category { get; set; }
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public int CurrentStock { get; set; }
        public int ReorderLevel { get; set; }
    }

    public class MaintenanceStatistics
    {
        public int TotalRequests { get; set; }
        public int CompletedRequests { get; set; }
        public int PendingRequests { get; set; }
        public int InProgressRequests { get; set; }
        public int EmergencyRequests { get; set; }
        public decimal TotalCost { get; set; }
        public double AverageResolutionHours { get; set; }
        public Dictionary<string, int> RequestsByCategory { get; set; }
        public Dictionary<string, decimal> CostByCategory { get; set; }
    }

    public class TechnicianPerformance
    {
        public int TechnicianId { get; set; }
        public string TechnicianName { get; set; }
        public int TotalAssigned { get; set; }
        public int Completed { get; set; }
        public double AverageCompletionHours { get; set; }
        public decimal TotalCostHandled { get; set; }
        public double CompletionRate => TotalAssigned > 0 ? (double)Completed / TotalAssigned * 100 : 0;
    }

    public class RoomMaintenanceHistory
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; }
        public int TotalRequests { get; set; }
        public decimal TotalCost { get; set; }
        public DateTime? LastRequestDate { get; set; }
    }

    #endregion
}
