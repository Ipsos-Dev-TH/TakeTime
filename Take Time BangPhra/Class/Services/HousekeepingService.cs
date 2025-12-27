using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// Housekeeping Management Service
    /// Manages room cleaning schedules, staff assignments, and room status
    /// </summary>
    public class HousekeepingService
    {
        private readonly string _connectionString;
        private readonly code _code;

        public HousekeepingService(string connectionString)
        {
            _connectionString = connectionString;
            _code = new code();
        }

        #region Room Status Management

        /// <summary>
        /// Get room status for all rooms
        /// </summary>
        public List<RoomStatus> GetAllRoomStatus(DateTime date)
        {
            var rooms = new List<RoomStatus>();

            var parameters = new Dictionary<string, object>
            {
                { "@date", date }
            };

            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    a.ID as AccommodationId,
                    a.Accommodation_Name as RoomName,
                    a.Room_Number as RoomNumber,
                    a.Floor,
                    ISNULL(rs.Status, 'VACANT_CLEAN') as Status,
                    ISNULL(rs.Last_Cleaned, a.Created_Date) as LastCleaned,
                    rs.Assigned_Staff_ID as AssignedStaffId,
                    e.Employee_Name as AssignedStaffName,
                    rs.Priority,
                    rs.Notes,
                    rs.Inspection_Status,
                    rs.Inspected_By,
                    rs.Inspection_Time,
                    -- Check if room is occupied
                    CASE WHEN EXISTS (
                        SELECT 1 FROM Reservation r
                        INNER JOIN Reservation_Accommodation ra ON r.ID = ra.Reservation_ID
                        WHERE ra.Accommodation_ID = a.ID
                          AND r.CheckinDate <= @date AND r.CheckoutDate > @date
                          AND r.Status IN (N'เช็คอินแล้ว', N'จองแล้ว')
                    ) THEN 1 ELSE 0 END as IsOccupied,
                    -- Check if checking out today
                    CASE WHEN EXISTS (
                        SELECT 1 FROM Reservation r
                        INNER JOIN Reservation_Accommodation ra ON r.ID = ra.Reservation_ID
                        WHERE ra.Accommodation_ID = a.ID
                          AND CAST(r.CheckoutDate AS DATE) = CAST(@date AS DATE)
                          AND r.Status = N'เช็คอินแล้ว'
                    ) THEN 1 ELSE 0 END as IsCheckoutToday,
                    -- Check if checking in today
                    CASE WHEN EXISTS (
                        SELECT 1 FROM Reservation r
                        INNER JOIN Reservation_Accommodation ra ON r.ID = ra.Reservation_ID
                        WHERE ra.Accommodation_ID = a.ID
                          AND CAST(r.CheckinDate AS DATE) = CAST(@date AS DATE)
                          AND r.Status = N'จองแล้ว'
                    ) THEN 1 ELSE 0 END as IsCheckinToday
                  FROM Accommodation a
                  LEFT JOIN Room_Status rs ON a.ID = rs.Accommodation_ID AND CAST(rs.Status_Date AS DATE) = CAST(@date AS DATE)
                  LEFT JOIN Employee e ON rs.Assigned_Staff_ID = e.ID
                  WHERE a.Status = 'True'
                  ORDER BY a.Floor, a.Room_Number",
                parameters);

            foreach (DataRow row in dt.Rows)
            {
                rooms.Add(new RoomStatus
                {
                    AccommodationId = Convert.ToInt32(row["AccommodationId"]),
                    RoomName = row["RoomName"].ToString(),
                    RoomNumber = row["RoomNumber"]?.ToString(),
                    Floor = row["Floor"]?.ToString(),
                    Status = row["Status"].ToString(),
                    LastCleaned = row["LastCleaned"] != DBNull.Value
                        ? Convert.ToDateTime(row["LastCleaned"])
                        : DateTime.MinValue,
                    AssignedStaffId = row["AssignedStaffId"] != DBNull.Value
                        ? Convert.ToInt32(row["AssignedStaffId"])
                        : (int?)null,
                    AssignedStaffName = row["AssignedStaffName"]?.ToString(),
                    Priority = row["Priority"] != DBNull.Value
                        ? Convert.ToInt32(row["Priority"])
                        : 5,
                    Notes = row["Notes"]?.ToString(),
                    InspectionStatus = row["Inspection_Status"]?.ToString(),
                    IsOccupied = Convert.ToBoolean(row["IsOccupied"]),
                    IsCheckoutToday = Convert.ToBoolean(row["IsCheckoutToday"]),
                    IsCheckinToday = Convert.ToBoolean(row["IsCheckinToday"])
                });
            }

            return rooms;
        }

        /// <summary>
        /// Update room status
        /// </summary>
        public bool UpdateRoomStatus(int accommodationId, string status, int? staffId = null, string notes = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@accommodationId", accommodationId },
                    { "@status", status },
                    { "@staffId", (object)staffId ?? DBNull.Value },
                    { "@notes", notes ?? "" },
                    { "@date", DateTime.Today }
                };

                // Check if record exists for today
                DataTable existing = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID FROM Room_Status
                      WHERE Accommodation_ID = @accommodationId AND CAST(Status_Date AS DATE) = CAST(@date AS DATE)",
                    parameters);

                if (existing.Rows.Count > 0)
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Room_Status SET
                            Status = @status,
                            Assigned_Staff_ID = @staffId,
                            Notes = @notes,
                            Modified_Date = GETDATE(),
                            Last_Cleaned = CASE WHEN @status = 'VACANT_CLEAN' THEN GETDATE() ELSE Last_Cleaned END
                          WHERE Accommodation_ID = @accommodationId AND CAST(Status_Date AS DATE) = CAST(@date AS DATE)",
                        parameters);
                }
                else
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"INSERT INTO Room_Status
                            (Accommodation_ID, Status, Status_Date, Assigned_Staff_ID, Notes, Created_Date, Last_Cleaned)
                          VALUES
                            (@accommodationId, @status, @date, @staffId, @notes, GETDATE(),
                             CASE WHEN @status = 'VACANT_CLEAN' THEN GETDATE() ELSE NULL END)",
                        parameters);
                }

                // Log activity
                _code.Logs(_connectionString, "Housekeeping",
                    $"Room {accommodationId} status changed to {status}", staffId?.ToString() ?? "SYSTEM");

                return true;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Housekeeping Error", ex.Message, "SYSTEM");
                return false;
            }
        }

        #endregion

        #region Task Management

        /// <summary>
        /// Create housekeeping task
        /// </summary>
        public int CreateTask(HousekeepingTask task)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@accommodationId", task.AccommodationId },
                    { "@taskType", task.TaskType },
                    { "@priority", task.Priority },
                    { "@assignedStaffId", (object)task.AssignedStaffId ?? DBNull.Value },
                    { "@scheduledDate", task.ScheduledDate },
                    { "@scheduledTime", (object)task.ScheduledTime ?? DBNull.Value },
                    { "@description", task.Description ?? "" },
                    { "@specialInstructions", task.SpecialInstructions ?? "" },
                    { "@estimatedMinutes", task.EstimatedMinutes }
                };

                _code.DatabaseInsertSafe(_connectionString,
                    @"INSERT INTO Housekeeping_Tasks
                        (Accommodation_ID, Task_Type, Priority, Assigned_Staff_ID,
                         Scheduled_Date, Scheduled_Time, Description, Special_Instructions,
                         Estimated_Minutes, Status, Created_Date)
                      VALUES
                        (@accommodationId, @taskType, @priority, @assignedStaffId,
                         @scheduledDate, @scheduledTime, @description, @specialInstructions,
                         @estimatedMinutes, 'PENDING', GETDATE());
                      SELECT SCOPE_IDENTITY();",
                    parameters);

                // Get new task ID
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT MAX(ID) as ID FROM Housekeeping_Tasks WHERE Accommodation_ID = @accommodationId",
                    parameters);

                return dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["ID"]) : 0;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Housekeeping Error", $"Create task failed: {ex.Message}", "SYSTEM");
                return 0;
            }
        }

        /// <summary>
        /// Get tasks for a specific date
        /// </summary>
        public List<HousekeepingTask> GetTasks(DateTime date, int? staffId = null, string status = null)
        {
            var tasks = new List<HousekeepingTask>();

            var parameters = new Dictionary<string, object>
            {
                { "@date", date },
                { "@staffId", (object)staffId ?? DBNull.Value },
                { "@status", status ?? "" }
            };

            string query = @"
                SELECT
                    t.ID, t.Accommodation_ID, a.Accommodation_Name, a.Room_Number,
                    t.Task_Type, t.Priority, t.Assigned_Staff_ID, e.Employee_Name as StaffName,
                    t.Scheduled_Date, t.Scheduled_Time, t.Description, t.Special_Instructions,
                    t.Estimated_Minutes, t.Actual_Minutes, t.Status,
                    t.Started_Time, t.Completed_Time, t.Notes
                FROM Housekeeping_Tasks t
                INNER JOIN Accommodation a ON t.Accommodation_ID = a.ID
                LEFT JOIN Employee e ON t.Assigned_Staff_ID = e.ID
                WHERE CAST(t.Scheduled_Date AS DATE) = CAST(@date AS DATE)";

            if (staffId.HasValue)
                query += " AND t.Assigned_Staff_ID = @staffId";

            if (!string.IsNullOrEmpty(status))
                query += " AND t.Status = @status";

            query += " ORDER BY t.Priority, t.Scheduled_Time";

            DataTable dt = _code.DatabaseQuerySafe(_connectionString, query, parameters);

            foreach (DataRow row in dt.Rows)
            {
                tasks.Add(new HousekeepingTask
                {
                    Id = Convert.ToInt32(row["ID"]),
                    AccommodationId = Convert.ToInt32(row["Accommodation_ID"]),
                    RoomName = row["Accommodation_Name"].ToString(),
                    RoomNumber = row["Room_Number"]?.ToString(),
                    TaskType = row["Task_Type"].ToString(),
                    Priority = Convert.ToInt32(row["Priority"]),
                    AssignedStaffId = row["Assigned_Staff_ID"] != DBNull.Value
                        ? Convert.ToInt32(row["Assigned_Staff_ID"]) : (int?)null,
                    AssignedStaffName = row["StaffName"]?.ToString(),
                    ScheduledDate = Convert.ToDateTime(row["Scheduled_Date"]),
                    ScheduledTime = row["Scheduled_Time"] != DBNull.Value
                        ? (TimeSpan?)TimeSpan.Parse(row["Scheduled_Time"].ToString()) : null,
                    Description = row["Description"]?.ToString(),
                    SpecialInstructions = row["Special_Instructions"]?.ToString(),
                    EstimatedMinutes = Convert.ToInt32(row["Estimated_Minutes"]),
                    ActualMinutes = row["Actual_Minutes"] != DBNull.Value
                        ? Convert.ToInt32(row["Actual_Minutes"]) : (int?)null,
                    Status = row["Status"].ToString(),
                    StartedTime = row["Started_Time"] != DBNull.Value
                        ? Convert.ToDateTime(row["Started_Time"]) : (DateTime?)null,
                    CompletedTime = row["Completed_Time"] != DBNull.Value
                        ? Convert.ToDateTime(row["Completed_Time"]) : (DateTime?)null,
                    Notes = row["Notes"]?.ToString()
                });
            }

            return tasks;
        }

        /// <summary>
        /// Start task
        /// </summary>
        public bool StartTask(int taskId, int staffId)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@taskId", taskId },
                    { "@staffId", staffId }
                };

                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Housekeeping_Tasks SET
                        Status = 'IN_PROGRESS',
                        Started_Time = GETDATE(),
                        Assigned_Staff_ID = @staffId
                      WHERE ID = @taskId",
                    parameters);

                // Update room status
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Accommodation_ID FROM Housekeeping_Tasks WHERE ID = @taskId",
                    parameters);

                if (dt.Rows.Count > 0)
                {
                    int accommodationId = Convert.ToInt32(dt.Rows[0]["Accommodation_ID"]);
                    UpdateRoomStatus(accommodationId, "CLEANING", staffId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Housekeeping Error", ex.Message, "SYSTEM");
                return false;
            }
        }

        /// <summary>
        /// Complete task
        /// </summary>
        public bool CompleteTask(int taskId, int staffId, string notes = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@taskId", taskId },
                    { "@staffId", staffId },
                    { "@notes", notes ?? "" }
                };

                // Get started time to calculate actual minutes
                DataTable dtTask = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Accommodation_ID, Started_Time FROM Housekeeping_Tasks WHERE ID = @taskId",
                    parameters);

                if (dtTask.Rows.Count > 0)
                {
                    int accommodationId = Convert.ToInt32(dtTask.Rows[0]["Accommodation_ID"]);
                    DateTime? startedTime = dtTask.Rows[0]["Started_Time"] != DBNull.Value
                        ? Convert.ToDateTime(dtTask.Rows[0]["Started_Time"])
                        : (DateTime?)null;

                    int actualMinutes = startedTime.HasValue
                        ? (int)(DateTime.Now - startedTime.Value).TotalMinutes
                        : 0;

                    parameters.Add("@actualMinutes", actualMinutes);

                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Housekeeping_Tasks SET
                            Status = 'COMPLETED',
                            Completed_Time = GETDATE(),
                            Actual_Minutes = @actualMinutes,
                            Notes = @notes
                          WHERE ID = @taskId",
                        parameters);

                    // Update room status to clean (pending inspection)
                    UpdateRoomStatus(accommodationId, "VACANT_DIRTY_INSPECTING", staffId, "รอตรวจสอบ");
                }

                return true;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Housekeeping Error", ex.Message, "SYSTEM");
                return false;
            }
        }

        /// <summary>
        /// Inspect room
        /// </summary>
        public bool InspectRoom(int accommodationId, int inspectorId, bool passed, string notes = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@accommodationId", accommodationId },
                    { "@inspectorId", inspectorId },
                    { "@status", passed ? "PASSED" : "FAILED" },
                    { "@notes", notes ?? "" },
                    { "@date", DateTime.Today }
                };

                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Room_Status SET
                        Inspection_Status = @status,
                        Inspected_By = @inspectorId,
                        Inspection_Time = GETDATE(),
                        Notes = @notes,
                        Status = CASE WHEN @status = 'PASSED' THEN 'VACANT_CLEAN' ELSE 'VACANT_DIRTY' END
                      WHERE Accommodation_ID = @accommodationId AND CAST(Status_Date AS DATE) = CAST(@date AS DATE)",
                    parameters);

                // If failed, create re-cleaning task
                if (!passed)
                {
                    CreateTask(new HousekeepingTask
                    {
                        AccommodationId = accommodationId,
                        TaskType = "RE_CLEAN",
                        Priority = 1, // High priority
                        ScheduledDate = DateTime.Today,
                        Description = "ทำความสะอาดซ้ำ - ไม่ผ่านการตรวจสอบ",
                        SpecialInstructions = notes,
                        EstimatedMinutes = 30
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Housekeeping Error", ex.Message, "SYSTEM");
                return false;
            }
        }

        #endregion

        #region Auto-Generation

        /// <summary>
        /// Generate daily cleaning tasks based on checkouts
        /// </summary>
        public int GenerateDailyTasks(DateTime date)
        {
            int createdCount = 0;

            var parameters = new Dictionary<string, object>
            {
                { "@date", date }
            };

            // Get rooms checking out today
            DataTable dtCheckouts = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT DISTINCT ra.Accommodation_ID, a.Accommodation_Name
                  FROM Reservation r
                  INNER JOIN Reservation_Accommodation ra ON r.ID = ra.Reservation_ID
                  INNER JOIN Accommodation a ON ra.Accommodation_ID = a.ID
                  WHERE CAST(r.CheckoutDate AS DATE) = CAST(@date AS DATE)
                    AND r.Status = N'เช็คอินแล้ว'",
                parameters);

            foreach (DataRow row in dtCheckouts.Rows)
            {
                int accommodationId = Convert.ToInt32(row["Accommodation_ID"]);

                // Check if task already exists
                var checkParams = new Dictionary<string, object>
                {
                    { "@accommodationId", accommodationId },
                    { "@date", date },
                    { "@taskType", "CHECKOUT_CLEAN" }
                };

                DataTable existing = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID FROM Housekeeping_Tasks
                      WHERE Accommodation_ID = @accommodationId
                        AND CAST(Scheduled_Date AS DATE) = CAST(@date AS DATE)
                        AND Task_Type = @taskType",
                    checkParams);

                if (existing.Rows.Count == 0)
                {
                    CreateTask(new HousekeepingTask
                    {
                        AccommodationId = accommodationId,
                        TaskType = "CHECKOUT_CLEAN",
                        Priority = 2,
                        ScheduledDate = date,
                        Description = "ทำความสะอาดหลังเช็คเอาท์",
                        EstimatedMinutes = 45
                    });
                    createdCount++;
                }
            }

            // Get rooms with guests staying (daily cleaning)
            DataTable dtStaying = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT DISTINCT ra.Accommodation_ID
                  FROM Reservation r
                  INNER JOIN Reservation_Accommodation ra ON r.ID = ra.Reservation_ID
                  WHERE r.CheckinDate <= @date AND r.CheckoutDate > @date
                    AND r.Status = N'เช็คอินแล้ว'",
                parameters);

            foreach (DataRow row in dtStaying.Rows)
            {
                int accommodationId = Convert.ToInt32(row["Accommodation_ID"]);

                var checkParams = new Dictionary<string, object>
                {
                    { "@accommodationId", accommodationId },
                    { "@date", date },
                    { "@taskType", "DAILY_CLEAN" }
                };

                DataTable existing = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID FROM Housekeeping_Tasks
                      WHERE Accommodation_ID = @accommodationId
                        AND CAST(Scheduled_Date AS DATE) = CAST(@date AS DATE)
                        AND Task_Type = @taskType",
                    checkParams);

                if (existing.Rows.Count == 0)
                {
                    CreateTask(new HousekeepingTask
                    {
                        AccommodationId = accommodationId,
                        TaskType = "DAILY_CLEAN",
                        Priority = 3,
                        ScheduledDate = date,
                        Description = "ทำความสะอาดประจำวัน",
                        EstimatedMinutes = 20
                    });
                    createdCount++;
                }
            }

            _code.Logs(_connectionString, "Housekeeping",
                $"Generated {createdCount} daily tasks for {date:dd/MM/yyyy}", "SYSTEM");

            return createdCount;
        }

        #endregion

        #region Staff Workload

        /// <summary>
        /// Get staff workload summary
        /// </summary>
        public List<StaffWorkload> GetStaffWorkload(DateTime date)
        {
            var workloads = new List<StaffWorkload>();

            var parameters = new Dictionary<string, object>
            {
                { "@date", date }
            };

            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    e.ID as StaffId,
                    e.Employee_Name as StaffName,
                    COUNT(t.ID) as TotalTasks,
                    SUM(CASE WHEN t.Status = 'PENDING' THEN 1 ELSE 0 END) as PendingTasks,
                    SUM(CASE WHEN t.Status = 'IN_PROGRESS' THEN 1 ELSE 0 END) as InProgressTasks,
                    SUM(CASE WHEN t.Status = 'COMPLETED' THEN 1 ELSE 0 END) as CompletedTasks,
                    SUM(t.Estimated_Minutes) as TotalEstimatedMinutes,
                    SUM(ISNULL(t.Actual_Minutes, 0)) as TotalActualMinutes
                  FROM Employee e
                  LEFT JOIN Housekeeping_Tasks t ON e.ID = t.Assigned_Staff_ID
                    AND CAST(t.Scheduled_Date AS DATE) = CAST(@date AS DATE)
                  WHERE e.Department = N'Housekeeping' AND e.Status = 'Active'
                  GROUP BY e.ID, e.Employee_Name
                  ORDER BY e.Employee_Name",
                parameters);

            foreach (DataRow row in dt.Rows)
            {
                workloads.Add(new StaffWorkload
                {
                    StaffId = Convert.ToInt32(row["StaffId"]),
                    StaffName = row["StaffName"].ToString(),
                    TotalTasks = Convert.ToInt32(row["TotalTasks"]),
                    PendingTasks = Convert.ToInt32(row["PendingTasks"]),
                    InProgressTasks = Convert.ToInt32(row["InProgressTasks"]),
                    CompletedTasks = Convert.ToInt32(row["CompletedTasks"]),
                    TotalEstimatedMinutes = row["TotalEstimatedMinutes"] != DBNull.Value
                        ? Convert.ToInt32(row["TotalEstimatedMinutes"]) : 0,
                    TotalActualMinutes = row["TotalActualMinutes"] != DBNull.Value
                        ? Convert.ToInt32(row["TotalActualMinutes"]) : 0
                });
            }

            return workloads;
        }

        /// <summary>
        /// Auto-assign tasks to staff based on workload
        /// </summary>
        public int AutoAssignTasks(DateTime date)
        {
            int assignedCount = 0;

            // Get unassigned tasks
            var parameters = new Dictionary<string, object>
            {
                { "@date", date }
            };

            DataTable dtTasks = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT ID, Estimated_Minutes, Priority
                  FROM Housekeeping_Tasks
                  WHERE CAST(Scheduled_Date AS DATE) = CAST(@date AS DATE)
                    AND Assigned_Staff_ID IS NULL AND Status = 'PENDING'
                  ORDER BY Priority, ID",
                parameters);

            // Get available staff with workload
            var workloads = GetStaffWorkload(date);
            if (workloads.Count == 0) return 0;

            foreach (DataRow taskRow in dtTasks.Rows)
            {
                int taskId = Convert.ToInt32(taskRow["ID"]);
                int estimatedMinutes = Convert.ToInt32(taskRow["Estimated_Minutes"]);

                // Find staff with lowest workload
                var selectedStaff = workloads
                    .OrderBy(w => w.TotalEstimatedMinutes)
                    .FirstOrDefault();

                if (selectedStaff != null)
                {
                    var assignParams = new Dictionary<string, object>
                    {
                        { "@taskId", taskId },
                        { "@staffId", selectedStaff.StaffId }
                    };

                    _code.DatabaseInsertSafe(_connectionString,
                        "UPDATE Housekeeping_Tasks SET Assigned_Staff_ID = @staffId WHERE ID = @taskId",
                        assignParams);

                    selectedStaff.TotalEstimatedMinutes += estimatedMinutes;
                    selectedStaff.TotalTasks++;
                    assignedCount++;
                }
            }

            return assignedCount;
        }

        #endregion

        #region Inventory (Supplies)

        /// <summary>
        /// Log supply usage
        /// </summary>
        public bool LogSupplyUsage(int taskId, List<SupplyUsage> supplies)
        {
            try
            {
                foreach (var supply in supplies)
                {
                    var parameters = new Dictionary<string, object>
                    {
                        { "@taskId", taskId },
                        { "@supplyId", supply.SupplyId },
                        { "@quantity", supply.Quantity }
                    };

                    _code.DatabaseInsertSafe(_connectionString,
                        @"INSERT INTO Housekeeping_Supply_Usage (Task_ID, Supply_ID, Quantity, Used_Date)
                          VALUES (@taskId, @supplyId, @quantity, GETDATE())",
                        parameters);

                    // Update supply stock
                    _code.DatabaseInsertSafe(_connectionString,
                        "UPDATE Housekeeping_Supplies SET Current_Stock = Current_Stock - @quantity WHERE ID = @supplyId",
                        parameters);
                }

                return true;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "Housekeeping Error", ex.Message, "SYSTEM");
                return false;
            }
        }

        /// <summary>
        /// Get low stock supplies
        /// </summary>
        public List<HousekeepingSupply> GetLowStockSupplies()
        {
            var supplies = new List<HousekeepingSupply>();

            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT ID, Supply_Name, Current_Stock, Min_Stock_Level, Unit
                  FROM Housekeeping_Supplies
                  WHERE Current_Stock <= Min_Stock_Level AND Is_Active = 1
                  ORDER BY (Current_Stock * 1.0 / NULLIF(Min_Stock_Level, 0))",
                new Dictionary<string, object>());

            foreach (DataRow row in dt.Rows)
            {
                supplies.Add(new HousekeepingSupply
                {
                    Id = Convert.ToInt32(row["ID"]),
                    SupplyName = row["Supply_Name"].ToString(),
                    CurrentStock = Convert.ToInt32(row["Current_Stock"]),
                    MinStockLevel = Convert.ToInt32(row["Min_Stock_Level"]),
                    Unit = row["Unit"].ToString()
                });
            }

            return supplies;
        }

        #endregion
    }

    #region Data Models

    public class RoomStatus
    {
        public int AccommodationId { get; set; }
        public string RoomName { get; set; }
        public string RoomNumber { get; set; }
        public string Floor { get; set; }
        public string Status { get; set; } // VACANT_CLEAN, VACANT_DIRTY, OCCUPIED, CLEANING, OUT_OF_ORDER
        public DateTime LastCleaned { get; set; }
        public int? AssignedStaffId { get; set; }
        public string AssignedStaffName { get; set; }
        public int Priority { get; set; }
        public string Notes { get; set; }
        public string InspectionStatus { get; set; }
        public bool IsOccupied { get; set; }
        public bool IsCheckoutToday { get; set; }
        public bool IsCheckinToday { get; set; }

        public string StatusDisplay
        {
            get
            {
                switch (Status)
                {
                    case "VACANT_CLEAN": return "ว่าง - สะอาด";
                    case "VACANT_DIRTY": return "ว่าง - รอทำความสะอาด";
                    case "OCCUPIED": return "มีแขกพัก";
                    case "CLEANING": return "กำลังทำความสะอาด";
                    case "VACANT_DIRTY_INSPECTING": return "รอตรวจสอบ";
                    case "OUT_OF_ORDER": return "ปิดปรับปรุง";
                    default: return Status;
                }
            }
        }

        public string StatusColor
        {
            get
            {
                switch (Status)
                {
                    case "VACANT_CLEAN": return "#4caf50";
                    case "VACANT_DIRTY": return "#f44336";
                    case "OCCUPIED": return "#2196f3";
                    case "CLEANING": return "#ff9800";
                    case "VACANT_DIRTY_INSPECTING": return "#9c27b0";
                    case "OUT_OF_ORDER": return "#9e9e9e";
                    default: return "#757575";
                }
            }
        }
    }

    public class HousekeepingTask
    {
        public int Id { get; set; }
        public int AccommodationId { get; set; }
        public string RoomName { get; set; }
        public string RoomNumber { get; set; }
        public string TaskType { get; set; } // CHECKOUT_CLEAN, DAILY_CLEAN, DEEP_CLEAN, TURNDOWN, RE_CLEAN
        public int Priority { get; set; }
        public int? AssignedStaffId { get; set; }
        public string AssignedStaffName { get; set; }
        public DateTime ScheduledDate { get; set; }
        public TimeSpan? ScheduledTime { get; set; }
        public string Description { get; set; }
        public string SpecialInstructions { get; set; }
        public int EstimatedMinutes { get; set; }
        public int? ActualMinutes { get; set; }
        public string Status { get; set; } // PENDING, IN_PROGRESS, COMPLETED, CANCELLED
        public DateTime? StartedTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public string Notes { get; set; }

        public string TaskTypeDisplay
        {
            get
            {
                switch (TaskType)
                {
                    case "CHECKOUT_CLEAN": return "ทำความสะอาดหลังเช็คเอาท์";
                    case "DAILY_CLEAN": return "ทำความสะอาดประจำวัน";
                    case "DEEP_CLEAN": return "ทำความสะอาดใหญ่";
                    case "TURNDOWN": return "เปิดเตียง";
                    case "RE_CLEAN": return "ทำความสะอาดซ้ำ";
                    default: return TaskType;
                }
            }
        }
    }

    public class StaffWorkload
    {
        public int StaffId { get; set; }
        public string StaffName { get; set; }
        public int TotalTasks { get; set; }
        public int PendingTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int TotalEstimatedMinutes { get; set; }
        public int TotalActualMinutes { get; set; }

        public decimal CompletionRate => TotalTasks > 0 ? (decimal)CompletedTasks / TotalTasks * 100 : 0;
        public decimal Efficiency => TotalEstimatedMinutes > 0 && TotalActualMinutes > 0
            ? (decimal)TotalEstimatedMinutes / TotalActualMinutes * 100 : 0;
    }

    public class SupplyUsage
    {
        public int SupplyId { get; set; }
        public int Quantity { get; set; }
    }

    public class HousekeepingSupply
    {
        public int Id { get; set; }
        public string SupplyName { get; set; }
        public int CurrentStock { get; set; }
        public int MinStockLevel { get; set; }
        public string Unit { get; set; }
    }

    #endregion
}
