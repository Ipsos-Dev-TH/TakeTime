using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;

namespace Take_Time_BangPhra.Class.Services
{
    /// <summary>
    /// Service for managing notifications (in-app, email, SMS, push)
    /// </summary>
    public class NotificationService
    {
        private readonly string _connectionString;
        private readonly SmtpSettings _smtpSettings;
        private readonly SmsSettings _smsSettings;

        public NotificationService(string connectionString)
        {
            _connectionString = connectionString;
            _smtpSettings = GetSmtpSettings();
            _smsSettings = GetSmsSettings();
        }

        #region In-App Notifications

        /// <summary>
        /// Create a notification
        /// </summary>
        public int CreateNotification(Notification notification)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"INSERT INTO Notifications
                    (UserId, UserType, Title, Message, NotificationType, Priority,
                     RelatedEntityType, RelatedEntityId, ActionUrl, IsRead, CreatedAt, ExpiresAt)
                    VALUES
                    (@UserId, @UserType, @Title, @Message, @NotificationType, @Priority,
                     @RelatedEntityType, @RelatedEntityId, @ActionUrl, 0, GETDATE(), @ExpiresAt);
                    SELECT SCOPE_IDENTITY();";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", notification.UserId);
                    cmd.Parameters.AddWithValue("@UserType", notification.UserType);
                    cmd.Parameters.AddWithValue("@Title", notification.Title);
                    cmd.Parameters.AddWithValue("@Message", notification.Message);
                    cmd.Parameters.AddWithValue("@NotificationType", notification.NotificationType);
                    cmd.Parameters.AddWithValue("@Priority", notification.Priority);
                    cmd.Parameters.AddWithValue("@RelatedEntityType", notification.RelatedEntityType ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@RelatedEntityId", notification.RelatedEntityId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ActionUrl", notification.ActionUrl ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ExpiresAt", notification.ExpiresAt ?? (object)DBNull.Value);

                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        /// <summary>
        /// Get notifications for a user
        /// </summary>
        public List<Notification> GetUserNotifications(int userId, string userType, bool unreadOnly = false, int limit = 50)
        {
            List<Notification> notifications = new List<Notification>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = $@"SELECT TOP (@Limit) * FROM Notifications
                    WHERE UserId = @UserId AND UserType = @UserType
                    AND (ExpiresAt IS NULL OR ExpiresAt > GETDATE())
                    {(unreadOnly ? "AND IsRead = 0" : "")}
                    ORDER BY CreatedAt DESC";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@UserType", userType);
                    cmd.Parameters.AddWithValue("@Limit", limit);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            notifications.Add(MapNotificationFromReader(reader));
                        }
                    }
                }
            }

            return notifications;
        }

        /// <summary>
        /// Get unread notification count
        /// </summary>
        public int GetUnreadCount(int userId, string userType)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"SELECT COUNT(*) FROM Notifications
                    WHERE UserId = @UserId AND UserType = @UserType AND IsRead = 0
                    AND (ExpiresAt IS NULL OR ExpiresAt > GETDATE())";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@UserType", userType);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        /// <summary>
        /// Mark notification as read
        /// </summary>
        public bool MarkAsRead(int notificationId, int userId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"UPDATE Notifications SET IsRead = 1, ReadAt = GETDATE()
                    WHERE NotificationId = @NotificationId AND UserId = @UserId";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@NotificationId", notificationId);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        /// <summary>
        /// Mark all notifications as read
        /// </summary>
        public int MarkAllAsRead(int userId, string userType)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"UPDATE Notifications SET IsRead = 1, ReadAt = GETDATE()
                    WHERE UserId = @UserId AND UserType = @UserType AND IsRead = 0";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@UserType", userType);
                    return cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Delete a notification
        /// </summary>
        public bool DeleteNotification(int notificationId, int userId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"DELETE FROM Notifications
                    WHERE NotificationId = @NotificationId AND UserId = @UserId";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@NotificationId", notificationId);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        /// <summary>
        /// Clean up expired notifications
        /// </summary>
        public int CleanupExpiredNotifications()
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"DELETE FROM Notifications
                    WHERE ExpiresAt IS NOT NULL AND ExpiresAt < GETDATE()";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    return cmd.ExecuteNonQuery();
                }
            }
        }

        private Notification MapNotificationFromReader(SqlDataReader reader)
        {
            return new Notification
            {
                NotificationId = Convert.ToInt32(reader["NotificationId"]),
                UserId = Convert.ToInt32(reader["UserId"]),
                UserType = reader["UserType"].ToString(),
                Title = reader["Title"].ToString(),
                Message = reader["Message"].ToString(),
                NotificationType = reader["NotificationType"].ToString(),
                Priority = reader["Priority"].ToString(),
                RelatedEntityType = reader["RelatedEntityType"]?.ToString(),
                RelatedEntityId = reader["RelatedEntityId"] != DBNull.Value ? Convert.ToInt32(reader["RelatedEntityId"]) : (int?)null,
                ActionUrl = reader["ActionUrl"]?.ToString(),
                IsRead = Convert.ToBoolean(reader["IsRead"]),
                ReadAt = reader["ReadAt"] != DBNull.Value ? Convert.ToDateTime(reader["ReadAt"]) : (DateTime?)null,
                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                ExpiresAt = reader["ExpiresAt"] != DBNull.Value ? Convert.ToDateTime(reader["ExpiresAt"]) : (DateTime?)null
            };
        }

        #endregion

        #region Email Notifications

        /// <summary>
        /// Send email notification
        /// </summary>
        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, string toName = null)
        {
            try
            {
                using (var client = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port))
                {
                    client.EnableSsl = _smtpSettings.EnableSsl;
                    client.Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password);

                    var message = new MailMessage
                    {
                        From = new MailAddress(_smtpSettings.FromEmail, _smtpSettings.FromName),
                        Subject = subject,
                        Body = htmlBody,
                        IsBodyHtml = true
                    };

                    message.To.Add(new MailAddress(toEmail, toName));
                    await client.SendMailAsync(message);

                    // Log email sent
                    LogEmailSent(toEmail, subject, true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogEmailSent(toEmail, subject, false, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Send reservation confirmation email
        /// </summary>
        public async Task<bool> SendReservationConfirmationAsync(ReservationEmailData data)
        {
            string template = GetEmailTemplate("ReservationConfirmation");
            string body = ReplaceEmailPlaceholders(template, new Dictionary<string, string>
            {
                {"{{GuestName}}", data.GuestName},
                {"{{ReservationCode}}", data.ReservationCode},
                {"{{CheckInDate}}", data.CheckInDate.ToString("dd MMMM yyyy")},
                {"{{CheckOutDate}}", data.CheckOutDate.ToString("dd MMMM yyyy")},
                {"{{RoomType}}", data.RoomType},
                {"{{RoomName}}", data.RoomName},
                {"{{TotalNights}}", data.TotalNights.ToString()},
                {"{{TotalPrice}}", data.TotalPrice.ToString("N2")},
                {"{{SpecialRequests}}", data.SpecialRequests ?? "ไม่มี"},
                {"{{HotelName}}", "TakeTime BangPhra"},
                {"{{HotelAddress}}", "บางพระ ศรีราชา ชลบุรี"},
                {"{{HotelPhone}}", "038-xxx-xxx"}
            });

            return await SendEmailAsync(data.GuestEmail, $"ยืนยันการจอง #{data.ReservationCode} - TakeTime BangPhra", body, data.GuestName);
        }

        /// <summary>
        /// Send check-in reminder email
        /// </summary>
        public async Task<bool> SendCheckInReminderAsync(ReservationEmailData data)
        {
            string template = GetEmailTemplate("CheckInReminder");
            string body = ReplaceEmailPlaceholders(template, new Dictionary<string, string>
            {
                {"{{GuestName}}", data.GuestName},
                {"{{ReservationCode}}", data.ReservationCode},
                {"{{CheckInDate}}", data.CheckInDate.ToString("dd MMMM yyyy")},
                {"{{CheckInTime}}", "14:00 น."},
                {"{{RoomType}}", data.RoomType},
                {"{{HotelName}}", "TakeTime BangPhra"}
            });

            return await SendEmailAsync(data.GuestEmail, $"แจ้งเตือนเช็คอิน - {data.CheckInDate:dd MMM}", body, data.GuestName);
        }

        /// <summary>
        /// Send payment confirmation email
        /// </summary>
        public async Task<bool> SendPaymentConfirmationAsync(PaymentEmailData data)
        {
            string template = GetEmailTemplate("PaymentConfirmation");
            string body = ReplaceEmailPlaceholders(template, new Dictionary<string, string>
            {
                {"{{GuestName}}", data.GuestName},
                {"{{ReservationCode}}", data.ReservationCode},
                {"{{PaymentAmount}}", data.PaymentAmount.ToString("N2")},
                {"{{PaymentMethod}}", data.PaymentMethod},
                {"{{PaymentDate}}", data.PaymentDate.ToString("dd MMMM yyyy HH:mm")},
                {"{{TransactionId}}", data.TransactionId},
                {"{{HotelName}}", "TakeTime BangPhra"}
            });

            return await SendEmailAsync(data.GuestEmail, $"ยืนยันการชำระเงิน #{data.TransactionId}", body, data.GuestName);
        }

        private void LogEmailSent(string toEmail, string subject, bool success, string error = null)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"INSERT INTO EmailLogs
                    (ToEmail, Subject, Success, ErrorMessage, SentAt)
                    VALUES (@ToEmail, @Subject, @Success, @ErrorMessage, GETDATE())";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ToEmail", toEmail);
                    cmd.Parameters.AddWithValue("@Subject", subject);
                    cmd.Parameters.AddWithValue("@Success", success);
                    cmd.Parameters.AddWithValue("@ErrorMessage", error ?? (object)DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private string GetEmailTemplate(string templateName)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT TemplateBody FROM EmailTemplates WHERE TemplateName = @TemplateName AND IsActive = 1";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@TemplateName", templateName);
                    var result = cmd.ExecuteScalar();
                    if (result != null)
                        return result.ToString();
                }
            }

            // Return default template if not found in database
            return GetDefaultEmailTemplate(templateName);
        }

        private string GetDefaultEmailTemplate(string templateName)
        {
            switch (templateName)
            {
                case "ReservationConfirmation":
                    return @"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>ยืนยันการจองห้องพัก</h2>
                        <p>เรียน คุณ{{GuestName}},</p>
                        <p>ขอบคุณสำหรับการจองห้องพักกับ {{HotelName}}</p>
                        <div style='background: #f5f5f5; padding: 15px; margin: 15px 0;'>
                            <p><strong>รหัสการจอง:</strong> {{ReservationCode}}</p>
                            <p><strong>ห้อง:</strong> {{RoomType}} - {{RoomName}}</p>
                            <p><strong>วันเช็คอิน:</strong> {{CheckInDate}}</p>
                            <p><strong>วันเช็คเอาท์:</strong> {{CheckOutDate}}</p>
                            <p><strong>จำนวนคืน:</strong> {{TotalNights}} คืน</p>
                            <p><strong>ราคารวม:</strong> ฿{{TotalPrice}}</p>
                            <p><strong>คำขอพิเศษ:</strong> {{SpecialRequests}}</p>
                        </div>
                        <p>หากมีข้อสงสัย กรุณาติดต่อ {{HotelPhone}}</p>
                        <p>ด้วยความเคารพ,<br/>{{HotelName}}</p>
                    </body>
                    </html>";

                case "CheckInReminder":
                    return @"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>แจ้งเตือนการเช็คอิน</h2>
                        <p>เรียน คุณ{{GuestName}},</p>
                        <p>เราตั้งตารอต้อนรับคุณในวันพรุ่งนี้!</p>
                        <div style='background: #f5f5f5; padding: 15px; margin: 15px 0;'>
                            <p><strong>รหัสการจอง:</strong> {{ReservationCode}}</p>
                            <p><strong>วันเช็คอิน:</strong> {{CheckInDate}}</p>
                            <p><strong>เวลาเช็คอิน:</strong> {{CheckInTime}}</p>
                            <p><strong>ประเภทห้อง:</strong> {{RoomType}}</p>
                        </div>
                        <p>ด้วยความเคารพ,<br/>{{HotelName}}</p>
                    </body>
                    </html>";

                case "PaymentConfirmation":
                    return @"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>ยืนยันการชำระเงิน</h2>
                        <p>เรียน คุณ{{GuestName}},</p>
                        <p>เราได้รับการชำระเงินของคุณเรียบร้อยแล้ว</p>
                        <div style='background: #f5f5f5; padding: 15px; margin: 15px 0;'>
                            <p><strong>รหัสการจอง:</strong> {{ReservationCode}}</p>
                            <p><strong>จำนวนเงิน:</strong> ฿{{PaymentAmount}}</p>
                            <p><strong>วิธีการชำระ:</strong> {{PaymentMethod}}</p>
                            <p><strong>วันที่ชำระ:</strong> {{PaymentDate}}</p>
                            <p><strong>หมายเลขธุรกรรม:</strong> {{TransactionId}}</p>
                        </div>
                        <p>ด้วยความเคารพ,<br/>{{HotelName}}</p>
                    </body>
                    </html>";

                default:
                    return "";
            }
        }

        private string ReplaceEmailPlaceholders(string template, Dictionary<string, string> placeholders)
        {
            foreach (var placeholder in placeholders)
            {
                template = template.Replace(placeholder.Key, HttpUtility.HtmlEncode(placeholder.Value));
            }
            return template;
        }

        #endregion

        #region SMS Notifications

        /// <summary>
        /// Send SMS notification
        /// </summary>
        public async Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                // Normalize phone number
                phoneNumber = NormalizePhoneNumber(phoneNumber);

                // Use SMS API (e.g., ThaiBulkSMS, SMS2PRO, etc.)
                using (var client = new WebClient())
                {
                    string apiUrl = string.Format(_smsSettings.ApiUrl,
                        _smsSettings.ApiKey,
                        _smsSettings.ApiSecret,
                        Uri.EscapeDataString(phoneNumber),
                        Uri.EscapeDataString(message),
                        _smsSettings.SenderId);

                    string response = await client.DownloadStringTaskAsync(apiUrl);

                    bool success = response.Contains("\"status\":\"success\"") || response.Contains("\"status\":1");
                    LogSmsSent(phoneNumber, message, success, success ? null : response);
                    return success;
                }
            }
            catch (Exception ex)
            {
                LogSmsSent(phoneNumber, message, false, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Send reservation confirmation SMS
        /// </summary>
        public async Task<bool> SendReservationConfirmationSmsAsync(string phoneNumber, string guestName, string reservationCode, DateTime checkInDate)
        {
            string message = $"ยืนยันการจอง {reservationCode} สำหรับคุณ{guestName} เช็คอิน {checkInDate:dd/MM/yyyy} เวลา 14:00 น. - TakeTime BangPhra";
            return await SendSmsAsync(phoneNumber, message);
        }

        /// <summary>
        /// Send check-in reminder SMS
        /// </summary>
        public async Task<bool> SendCheckInReminderSmsAsync(string phoneNumber, string guestName, DateTime checkInDate)
        {
            string message = $"แจ้งเตือน: พรุ่งนี้เป็นวันเช็คอินของคุณ{guestName} เวลา 14:00 น. เป็นต้นไป - TakeTime BangPhra";
            return await SendSmsAsync(phoneNumber, message);
        }

        private string NormalizePhoneNumber(string phoneNumber)
        {
            // Remove spaces, dashes
            phoneNumber = phoneNumber.Replace(" ", "").Replace("-", "");

            // Convert 0xxxxxxxxx to 66xxxxxxxxx (Thai format)
            if (phoneNumber.StartsWith("0"))
                phoneNumber = "66" + phoneNumber.Substring(1);

            // Remove leading + if present
            if (phoneNumber.StartsWith("+"))
                phoneNumber = phoneNumber.Substring(1);

            return phoneNumber;
        }

        private void LogSmsSent(string phoneNumber, string message, bool success, string error = null)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"INSERT INTO SmsLogs
                    (PhoneNumber, Message, Success, ErrorMessage, SentAt)
                    VALUES (@PhoneNumber, @Message, @Success, @ErrorMessage, GETDATE())";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@PhoneNumber", phoneNumber);
                    cmd.Parameters.AddWithValue("@Message", message);
                    cmd.Parameters.AddWithValue("@Success", success);
                    cmd.Parameters.AddWithValue("@ErrorMessage", error ?? (object)DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion

        #region Broadcast Notifications

        /// <summary>
        /// Send notification to all staff
        /// </summary>
        public void NotifyAllStaff(string title, string message, string notificationType, string priority = "NORMAL")
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"INSERT INTO Notifications
                    (UserId, UserType, Title, Message, NotificationType, Priority, IsRead, CreatedAt)
                    SELECT Id, 'EMPLOYEE', @Title, @Message, @NotificationType, @Priority, 0, GETDATE()
                    FROM Employees WHERE IsActive = 1";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Title", title);
                    cmd.Parameters.AddWithValue("@Message", message);
                    cmd.Parameters.AddWithValue("@NotificationType", notificationType);
                    cmd.Parameters.AddWithValue("@Priority", priority);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Send notification to department
        /// </summary>
        public void NotifyDepartment(int departmentId, string title, string message, string notificationType, string priority = "NORMAL")
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"INSERT INTO Notifications
                    (UserId, UserType, Title, Message, NotificationType, Priority, IsRead, CreatedAt)
                    SELECT Id, 'EMPLOYEE', @Title, @Message, @NotificationType, @Priority, 0, GETDATE()
                    FROM Employees WHERE DepartmentId = @DepartmentId AND IsActive = 1";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@DepartmentId", departmentId);
                    cmd.Parameters.AddWithValue("@Title", title);
                    cmd.Parameters.AddWithValue("@Message", message);
                    cmd.Parameters.AddWithValue("@NotificationType", notificationType);
                    cmd.Parameters.AddWithValue("@Priority", priority);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Send notification to role
        /// </summary>
        public void NotifyRole(string roleName, string title, string message, string notificationType, string priority = "NORMAL")
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"INSERT INTO Notifications
                    (UserId, UserType, Title, Message, NotificationType, Priority, IsRead, CreatedAt)
                    SELECT e.Id, 'EMPLOYEE', @Title, @Message, @NotificationType, @Priority, 0, GETDATE()
                    FROM Employees e
                    INNER JOIN Roles r ON e.RoleId = r.Id
                    WHERE r.RoleName = @RoleName AND e.IsActive = 1";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@RoleName", roleName);
                    cmd.Parameters.AddWithValue("@Title", title);
                    cmd.Parameters.AddWithValue("@Message", message);
                    cmd.Parameters.AddWithValue("@NotificationType", notificationType);
                    cmd.Parameters.AddWithValue("@Priority", priority);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion

        #region Automatic Notifications

        /// <summary>
        /// Send check-in reminders for tomorrow's arrivals
        /// </summary>
        public async Task<int> SendTomorrowCheckInRemindersAsync()
        {
            int sentCount = 0;
            DateTime tomorrow = DateTime.Today.AddDays(1);

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"SELECT r.*, c.FirstName + ' ' + c.LastName as GuestName, c.Email, c.PhoneNumber,
                    a.AccommodationName as RoomType
                    FROM Reservations r
                    INNER JOIN Customer c ON r.CustomerId = c.Id
                    INNER JOIN Accommodations a ON r.AccommodationId = a.Id
                    WHERE CAST(r.CheckInDate AS DATE) = @Tomorrow
                    AND r.Status = 'Confirmed'
                    AND r.ReminderSent = 0";

                List<dynamic> reservations = new List<dynamic>();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Tomorrow", tomorrow);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            reservations.Add(new
                            {
                                ReservationId = Convert.ToInt32(reader["Id"]),
                                ReservationCode = reader["ReservationCode"].ToString(),
                                GuestName = reader["GuestName"].ToString(),
                                Email = reader["Email"]?.ToString(),
                                PhoneNumber = reader["PhoneNumber"]?.ToString(),
                                CheckInDate = Convert.ToDateTime(reader["CheckInDate"]),
                                RoomType = reader["RoomType"].ToString()
                            });
                        }
                    }
                }

                foreach (var res in reservations)
                {
                    bool emailSent = false, smsSent = false;

                    // Send email
                    if (!string.IsNullOrEmpty(res.Email))
                    {
                        var emailData = new ReservationEmailData
                        {
                            GuestName = res.GuestName,
                            GuestEmail = res.Email,
                            ReservationCode = res.ReservationCode,
                            CheckInDate = res.CheckInDate,
                            RoomType = res.RoomType
                        };
                        emailSent = await SendCheckInReminderAsync(emailData);
                    }

                    // Send SMS
                    if (!string.IsNullOrEmpty(res.PhoneNumber))
                    {
                        smsSent = await SendCheckInReminderSmsAsync(res.PhoneNumber, res.GuestName, res.CheckInDate);
                    }

                    // Mark as reminder sent
                    if (emailSent || smsSent)
                    {
                        string updateSql = "UPDATE Reservations SET ReminderSent = 1 WHERE Id = @Id";
                        using (SqlCommand updateCmd = new SqlCommand(updateSql, conn))
                        {
                            updateCmd.Parameters.AddWithValue("@Id", res.ReservationId);
                            updateCmd.ExecuteNonQuery();
                        }
                        sentCount++;
                    }
                }
            }

            return sentCount;
        }

        /// <summary>
        /// Send feedback request after checkout
        /// </summary>
        public async Task<int> SendPostCheckoutFeedbackRequestsAsync()
        {
            int sentCount = 0;
            DateTime yesterday = DateTime.Today.AddDays(-1);

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"SELECT r.*, c.FirstName + ' ' + c.LastName as GuestName, c.Email
                    FROM Reservations r
                    INNER JOIN Customer c ON r.CustomerId = c.Id
                    WHERE CAST(r.CheckOutDate AS DATE) = @Yesterday
                    AND r.Status = 'Completed'
                    AND r.FeedbackRequestSent = 0
                    AND c.Email IS NOT NULL";

                List<dynamic> reservations = new List<dynamic>();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Yesterday", yesterday);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            reservations.Add(new
                            {
                                ReservationId = Convert.ToInt32(reader["Id"]),
                                ReservationCode = reader["ReservationCode"].ToString(),
                                GuestName = reader["GuestName"].ToString(),
                                Email = reader["Email"].ToString()
                            });
                        }
                    }
                }

                foreach (var res in reservations)
                {
                    string feedbackUrl = $"https://taketime.example.com/feedback/{res.ReservationCode}";
                    string body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>ขอบคุณที่เข้าพักกับเรา</h2>
                        <p>เรียน คุณ{res.GuestName},</p>
                        <p>ขอบคุณที่เลือกเข้าพักที่ TakeTime BangPhra</p>
                        <p>เราอยากทราบความคิดเห็นของคุณเกี่ยวกับการเข้าพัก</p>
                        <p><a href='{feedbackUrl}' style='background: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>ให้คะแนนและรีวิว</a></p>
                        <p>ด้วยความเคารพ,<br/>TakeTime BangPhra</p>
                    </body>
                    </html>";

                    bool sent = await SendEmailAsync(res.Email, "ขอบคุณที่เข้าพัก - แบ่งปันประสบการณ์ของคุณ", body, res.GuestName);

                    if (sent)
                    {
                        string updateSql = "UPDATE Reservations SET FeedbackRequestSent = 1 WHERE Id = @Id";
                        using (SqlCommand updateCmd = new SqlCommand(updateSql, conn))
                        {
                            updateCmd.Parameters.AddWithValue("@Id", res.ReservationId);
                            updateCmd.ExecuteNonQuery();
                        }
                        sentCount++;
                    }
                }
            }

            return sentCount;
        }

        #endregion

        #region Settings

        private SmtpSettings GetSmtpSettings()
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT * FROM SystemSettings WHERE SettingGroup = 'SMTP'";

                var settings = new SmtpSettings();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string key = reader["SettingKey"].ToString();
                            string value = reader["SettingValue"].ToString();

                            switch (key)
                            {
                                case "SmtpHost": settings.Host = value; break;
                                case "SmtpPort": settings.Port = int.Parse(value); break;
                                case "SmtpUsername": settings.Username = value; break;
                                case "SmtpPassword": settings.Password = value; break;
                                case "SmtpEnableSsl": settings.EnableSsl = bool.Parse(value); break;
                                case "SmtpFromEmail": settings.FromEmail = value; break;
                                case "SmtpFromName": settings.FromName = value; break;
                            }
                        }
                    }
                }

                // Set defaults if not configured
                if (string.IsNullOrEmpty(settings.Host))
                {
                    settings.Host = "smtp.gmail.com";
                    settings.Port = 587;
                    settings.EnableSsl = true;
                }

                return settings;
            }
        }

        private SmsSettings GetSmsSettings()
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT * FROM SystemSettings WHERE SettingGroup = 'SMS'";

                var settings = new SmsSettings();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string key = reader["SettingKey"].ToString();
                            string value = reader["SettingValue"].ToString();

                            switch (key)
                            {
                                case "SmsApiUrl": settings.ApiUrl = value; break;
                                case "SmsApiKey": settings.ApiKey = value; break;
                                case "SmsApiSecret": settings.ApiSecret = value; break;
                                case "SmsSenderId": settings.SenderId = value; break;
                            }
                        }
                    }
                }

                return settings;
            }
        }

        #endregion
    }

    #region Data Models

    public class Notification
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; }
        public string UserType { get; set; } // EMPLOYEE, CUSTOMER, GUEST
        public string Title { get; set; }
        public string Message { get; set; }
        public string NotificationType { get; set; } // RESERVATION, PAYMENT, HOUSEKEEPING, MAINTENANCE, SYSTEM, PROMOTION
        public string Priority { get; set; } // LOW, NORMAL, HIGH, URGENT
        public string RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }
        public string ActionUrl { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class ReservationEmailData
    {
        public string GuestName { get; set; }
        public string GuestEmail { get; set; }
        public string ReservationCode { get; set; }
        public DateTime CheckInDate { get; set; }
        public DateTime CheckOutDate { get; set; }
        public string RoomType { get; set; }
        public string RoomName { get; set; }
        public int TotalNights { get; set; }
        public decimal TotalPrice { get; set; }
        public string SpecialRequests { get; set; }
    }

    public class PaymentEmailData
    {
        public string GuestName { get; set; }
        public string GuestEmail { get; set; }
        public string ReservationCode { get; set; }
        public decimal PaymentAmount { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime PaymentDate { get; set; }
        public string TransactionId { get; set; }
    }

    public class SmtpSettings
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool EnableSsl { get; set; }
        public string FromEmail { get; set; }
        public string FromName { get; set; }
    }

    public class SmsSettings
    {
        public string ApiUrl { get; set; }
        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }
        public string SenderId { get; set; }
    }

    #endregion
}
