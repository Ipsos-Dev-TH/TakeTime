using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// Channel Manager Service - Integration with OTA platforms
    /// Manages inventory synchronization with Agoda, Booking.com, Expedia, etc.
    /// </summary>
    public class ChannelManagerService
    {
        private readonly string _connectionString;
        private readonly code _code;
        private readonly HttpClient _httpClient;

        // Channel API configurations
        private readonly Dictionary<string, ChannelConfig> _channelConfigs;

        public ChannelManagerService(string connectionString)
        {
            _connectionString = connectionString;
            _code = new code();
            _httpClient = new HttpClient();

            _channelConfigs = LoadChannelConfigurations();
        }

        #region Channel Configuration

        /// <summary>
        /// Load channel API configurations from database
        /// </summary>
        private Dictionary<string, ChannelConfig> LoadChannelConfigurations()
        {
            var configs = new Dictionary<string, ChannelConfig>();

            try
            {
                var parameters = new Dictionary<string, object>();
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT Channel_Code, Channel_Name, API_Endpoint, API_Key, API_Secret,
                             Hotel_ID, Is_Active, Sync_Interval_Minutes
                      FROM OTA_Channels
                      WHERE Is_Active = 1",
                    parameters);

                foreach (DataRow row in dt.Rows)
                {
                    string code = row["Channel_Code"].ToString();
                    configs[code] = new ChannelConfig
                    {
                        ChannelCode = code,
                        ChannelName = row["Channel_Name"].ToString(),
                        ApiEndpoint = row["API_Endpoint"].ToString(),
                        ApiKey = row["API_Key"].ToString(),
                        ApiSecret = row["API_Secret"].ToString(),
                        HotelId = row["Hotel_ID"].ToString(),
                        IsActive = Convert.ToBoolean(row["Is_Active"]),
                        SyncIntervalMinutes = Convert.ToInt32(row["Sync_Interval_Minutes"])
                    };
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "ChannelManager", $"Error loading configs: {ex.Message}", "SYSTEM");
            }

            return configs;
        }

        #endregion

        #region Inventory Synchronization

        /// <summary>
        /// Sync availability to all active channels
        /// </summary>
        public async Task<SyncResult> SyncAvailabilityToAllChannels(DateTime startDate, DateTime endDate)
        {
            var result = new SyncResult { StartTime = DateTime.Now };
            var channelResults = new List<ChannelSyncResult>();

            foreach (var channel in _channelConfigs.Values)
            {
                if (!channel.IsActive) continue;

                try
                {
                    var channelResult = await SyncAvailabilityToChannel(channel.ChannelCode, startDate, endDate);
                    channelResults.Add(channelResult);
                }
                catch (Exception ex)
                {
                    channelResults.Add(new ChannelSyncResult
                    {
                        ChannelCode = channel.ChannelCode,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            result.EndTime = DateTime.Now;
            result.ChannelResults = channelResults;
            result.Success = channelResults.TrueForAll(r => r.Success);

            // Log sync result
            LogSyncResult(result);

            return result;
        }

        /// <summary>
        /// Sync availability to a specific channel
        /// </summary>
        public async Task<ChannelSyncResult> SyncAvailabilityToChannel(string channelCode, DateTime startDate, DateTime endDate)
        {
            var result = new ChannelSyncResult
            {
                ChannelCode = channelCode,
                SyncTime = DateTime.Now
            };

            try
            {
                if (!_channelConfigs.ContainsKey(channelCode))
                {
                    throw new Exception($"Channel {channelCode} not configured");
                }

                var config = _channelConfigs[channelCode];

                // Get availability data
                var availability = GetAvailabilityData(startDate, endDate);

                // Format data for specific channel
                var payload = FormatAvailabilityPayload(channelCode, availability);

                // Send to channel API
                var response = await SendToChannelApi(config, "availability", payload);

                result.Success = response.Success;
                result.Message = response.Message;
                result.RecordsSynced = availability.Count;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                _code.Logs(_connectionString, "ChannelManager",
                    $"Sync to {channelCode} failed: {ex.Message}", "SYSTEM");
            }

            return result;
        }

        /// <summary>
        /// Get availability data from local database
        /// </summary>
        private List<AvailabilityData> GetAvailabilityData(DateTime startDate, DateTime endDate)
        {
            var availability = new List<AvailabilityData>();

            var parameters = new Dictionary<string, object>
            {
                { "@startDate", startDate },
                { "@endDate", endDate }
            };

            // Get room types and their availability
            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    a.ID as RoomTypeId,
                    a.Accommodation_Name as RoomTypeName,
                    a.Capacity,
                    ISNULL(ap.Price, a.Price) as Price,
                    d.Date,
                    a.TotalRooms - ISNULL(booked.BookedCount, 0) as Available
                  FROM Accommodation a
                  CROSS JOIN (
                      SELECT DATEADD(DAY, number, @startDate) as Date
                      FROM master..spt_values
                      WHERE type = 'P' AND number <= DATEDIFF(DAY, @startDate, @endDate)
                  ) d
                  LEFT JOIN Accommodation_Price ap ON a.ID = ap.Accommodation_ID AND ap.Date = d.Date
                  LEFT JOIN (
                      SELECT ra.Accommodation_ID, r.CheckinDate, r.CheckoutDate, COUNT(*) as BookedCount
                      FROM Reservation_Accommodation ra
                      INNER JOIN Reservation r ON ra.Reservation_ID = r.ID
                      WHERE r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
                      GROUP BY ra.Accommodation_ID, r.CheckinDate, r.CheckoutDate
                  ) booked ON a.ID = booked.Accommodation_ID
                      AND d.Date >= booked.CheckinDate AND d.Date < booked.CheckoutDate
                  WHERE a.Status = 'True'
                  ORDER BY a.ID, d.Date",
                parameters);

            foreach (DataRow row in dt.Rows)
            {
                availability.Add(new AvailabilityData
                {
                    RoomTypeId = Convert.ToInt32(row["RoomTypeId"]),
                    RoomTypeName = row["RoomTypeName"].ToString(),
                    Date = Convert.ToDateTime(row["Date"]),
                    Available = Convert.ToInt32(row["Available"]),
                    Price = Convert.ToDecimal(row["Price"]),
                    Capacity = Convert.ToInt32(row["Capacity"])
                });
            }

            return availability;
        }

        /// <summary>
        /// Format availability payload for specific channel
        /// </summary>
        private string FormatAvailabilityPayload(string channelCode, List<AvailabilityData> availability)
        {
            switch (channelCode.ToUpper())
            {
                case "AGODA":
                    return FormatAgodaPayload(availability);
                case "BOOKING":
                    return FormatBookingPayload(availability);
                case "EXPEDIA":
                    return FormatExpediaPayload(availability);
                default:
                    return JsonConvert.SerializeObject(availability);
            }
        }

        private string FormatAgodaPayload(List<AvailabilityData> availability)
        {
            var agodaData = new
            {
                hotel_id = _channelConfigs["AGODA"]?.HotelId,
                availability = availability.Select(a => new
                {
                    room_type_id = a.RoomTypeId,
                    date = a.Date.ToString("yyyy-MM-dd"),
                    available_rooms = a.Available,
                    rate = a.Price
                }).ToList()
            };
            return JsonConvert.SerializeObject(agodaData);
        }

        private string FormatBookingPayload(List<AvailabilityData> availability)
        {
            var bookingData = new
            {
                hotelId = _channelConfigs["BOOKING"]?.HotelId,
                inventory = availability.Select(a => new
                {
                    roomId = a.RoomTypeId.ToString(),
                    date = a.Date.ToString("yyyy-MM-dd"),
                    count = a.Available,
                    price = new { amount = a.Price, currency = "THB" }
                }).ToList()
            };
            return JsonConvert.SerializeObject(bookingData);
        }

        private string FormatExpediaPayload(List<AvailabilityData> availability)
        {
            var expediaData = new
            {
                PropertyId = _channelConfigs["EXPEDIA"]?.HotelId,
                RoomAvailability = availability.Select(a => new
                {
                    RoomTypeID = a.RoomTypeId,
                    InventoryDate = a.Date.ToString("yyyy-MM-dd"),
                    AvailableCount = a.Available,
                    BaseRate = a.Price
                }).ToList()
            };
            return JsonConvert.SerializeObject(expediaData);
        }

        #endregion

        #region Rate Management

        /// <summary>
        /// Sync rates to all active channels
        /// </summary>
        public async Task<SyncResult> SyncRatesToAllChannels(DateTime startDate, DateTime endDate)
        {
            var result = new SyncResult { StartTime = DateTime.Now };
            var channelResults = new List<ChannelSyncResult>();

            foreach (var channel in _channelConfigs.Values)
            {
                if (!channel.IsActive) continue;

                try
                {
                    var channelResult = await SyncRatesToChannel(channel.ChannelCode, startDate, endDate);
                    channelResults.Add(channelResult);
                }
                catch (Exception ex)
                {
                    channelResults.Add(new ChannelSyncResult
                    {
                        ChannelCode = channel.ChannelCode,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            result.EndTime = DateTime.Now;
            result.ChannelResults = channelResults;
            result.Success = channelResults.TrueForAll(r => r.Success);

            return result;
        }

        /// <summary>
        /// Sync rates to a specific channel
        /// </summary>
        public async Task<ChannelSyncResult> SyncRatesToChannel(string channelCode, DateTime startDate, DateTime endDate)
        {
            var result = new ChannelSyncResult
            {
                ChannelCode = channelCode,
                SyncTime = DateTime.Now
            };

            try
            {
                if (!_channelConfigs.ContainsKey(channelCode))
                {
                    throw new Exception($"Channel {channelCode} not configured");
                }

                var config = _channelConfigs[channelCode];

                // Get rate data
                var rates = GetRateData(startDate, endDate);

                // Format data for specific channel
                var payload = FormatRatePayload(channelCode, rates);

                // Send to channel API
                var response = await SendToChannelApi(config, "rates", payload);

                result.Success = response.Success;
                result.Message = response.Message;
                result.RecordsSynced = rates.Count;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Get rate data from local database
        /// </summary>
        private List<RateData> GetRateData(DateTime startDate, DateTime endDate)
        {
            var rates = new List<RateData>();

            var parameters = new Dictionary<string, object>
            {
                { "@startDate", startDate },
                { "@endDate", endDate }
            };

            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    a.ID as RoomTypeId,
                    a.Accommodation_Name as RoomTypeName,
                    d.Date,
                    ISNULL(ap.Price, a.Price) as BaseRate,
                    ISNULL(dp.Rate_Multiplier, 1.0) as Multiplier,
                    ISNULL(ap.Price, a.Price) * ISNULL(dp.Rate_Multiplier, 1.0) as FinalRate
                  FROM Accommodation a
                  CROSS JOIN (
                      SELECT DATEADD(DAY, number, @startDate) as Date
                      FROM master..spt_values
                      WHERE type = 'P' AND number <= DATEDIFF(DAY, @startDate, @endDate)
                  ) d
                  LEFT JOIN Accommodation_Price ap ON a.ID = ap.Accommodation_ID AND ap.Date = d.Date
                  LEFT JOIN Dynamic_Pricing dp ON a.ID = dp.Accommodation_ID
                      AND d.Date >= dp.Start_Date AND d.Date <= dp.End_Date
                  WHERE a.Status = 'True'
                  ORDER BY a.ID, d.Date",
                parameters);

            foreach (DataRow row in dt.Rows)
            {
                rates.Add(new RateData
                {
                    RoomTypeId = Convert.ToInt32(row["RoomTypeId"]),
                    RoomTypeName = row["RoomTypeName"].ToString(),
                    Date = Convert.ToDateTime(row["Date"]),
                    BaseRate = Convert.ToDecimal(row["BaseRate"]),
                    FinalRate = Convert.ToDecimal(row["FinalRate"])
                });
            }

            return rates;
        }

        private string FormatRatePayload(string channelCode, List<RateData> rates)
        {
            return JsonConvert.SerializeObject(rates);
        }

        #endregion

        #region Reservation Import

        /// <summary>
        /// Import reservations from OTA channels
        /// </summary>
        public async Task<ImportResult> ImportReservationsFromChannel(string channelCode)
        {
            var result = new ImportResult { StartTime = DateTime.Now };

            try
            {
                if (!_channelConfigs.ContainsKey(channelCode))
                {
                    throw new Exception($"Channel {channelCode} not configured");
                }

                var config = _channelConfigs[channelCode];

                // Fetch reservations from channel API
                var response = await FetchFromChannelApi(config, "reservations");

                if (response.Success)
                {
                    var reservations = ParseReservations(channelCode, response.Data);

                    foreach (var reservation in reservations)
                    {
                        try
                        {
                            CreateLocalReservation(reservation, channelCode);
                            result.ImportedCount++;
                        }
                        catch (Exception ex)
                        {
                            result.FailedCount++;
                            result.Errors.Add($"Reservation {reservation.ChannelBookingId}: {ex.Message}");
                        }
                    }
                }

                result.Success = result.FailedCount == 0;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(ex.Message);
            }

            result.EndTime = DateTime.Now;
            return result;
        }

        /// <summary>
        /// Parse reservations from channel response
        /// </summary>
        private List<OTAReservation> ParseReservations(string channelCode, string data)
        {
            var reservations = new List<OTAReservation>();

            // Parse based on channel format
            switch (channelCode.ToUpper())
            {
                case "AGODA":
                case "BOOKING":
                case "EXPEDIA":
                    // Generic JSON parsing - actual implementation depends on each OTA's API format
                    var parsed = JsonConvert.DeserializeObject<List<OTAReservation>>(data);
                    if (parsed != null)
                        reservations = parsed;
                    break;
            }

            return reservations;
        }

        /// <summary>
        /// Create local reservation from OTA booking
        /// </summary>
        private void CreateLocalReservation(OTAReservation otaReservation, string channelCode)
        {
            // Check if already exists
            var checkParams = new Dictionary<string, object>
            {
                { "@channelBookingId", otaReservation.ChannelBookingId },
                { "@channelCode", channelCode }
            };

            var existing = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT ID FROM Reservation
                  WHERE OTA_Booking_ID = @channelBookingId AND OTA_Channel = @channelCode",
                checkParams);

            if (existing.Rows.Count > 0)
            {
                // Update existing
                UpdateOTAReservation(otaReservation, channelCode);
                return;
            }

            // Create customer if not exists
            EnsureCustomerExists(otaReservation);

            // Create reservation
            var parameters = new Dictionary<string, object>
            {
                { "@customerPhone", otaReservation.GuestPhone ?? "OTA_" + otaReservation.ChannelBookingId },
                { "@checkIn", otaReservation.CheckInDate },
                { "@checkOut", otaReservation.CheckOutDate },
                { "@totalPrice", otaReservation.TotalPrice },
                { "@deposit", otaReservation.TotalPrice }, // OTA bookings are prepaid
                { "@status", "จองแล้ว" },
                { "@notes", $"จองผ่าน {channelCode} - Booking ID: {otaReservation.ChannelBookingId}" },
                { "@otaChannel", channelCode },
                { "@otaBookingId", otaReservation.ChannelBookingId },
                { "@guestName", otaReservation.GuestName },
                { "@guestEmail", otaReservation.GuestEmail }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"INSERT INTO Reservation (
                    Customer_MobilePhone, CheckinDate, CheckoutDate, TotalPrice, Deposit,
                    Status, Notes, Created_Date, OTA_Channel, OTA_Booking_ID,
                    OTA_Guest_Name, OTA_Guest_Email
                  )
                  VALUES (
                    @customerPhone, @checkIn, @checkOut, @totalPrice, @deposit,
                    @status, @notes, GETDATE(), @otaChannel, @otaBookingId,
                    @guestName, @guestEmail
                  )",
                parameters);

            _code.Logs(_connectionString, "OTA Import",
                $"Created reservation from {channelCode}: {otaReservation.ChannelBookingId}", "SYSTEM");
        }

        private void UpdateOTAReservation(OTAReservation otaReservation, string channelCode)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@channelBookingId", otaReservation.ChannelBookingId },
                { "@channelCode", channelCode },
                { "@status", MapOTAStatus(otaReservation.Status) },
                { "@totalPrice", otaReservation.TotalPrice }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE Reservation
                  SET Status = @status, TotalPrice = @totalPrice, Modified_Date = GETDATE()
                  WHERE OTA_Booking_ID = @channelBookingId AND OTA_Channel = @channelCode",
                parameters);
        }

        private void EnsureCustomerExists(OTAReservation reservation)
        {
            string phone = reservation.GuestPhone ?? "OTA_" + reservation.ChannelBookingId;

            var checkParams = new Dictionary<string, object> { { "@phone", phone } };
            var existing = _code.DatabaseQuerySafe(_connectionString,
                "SELECT Customer_MobilePhone FROM Customer WHERE Customer_MobilePhone = @phone",
                checkParams);

            if (existing.Rows.Count == 0)
            {
                var insertParams = new Dictionary<string, object>
                {
                    { "@phone", phone },
                    { "@name", reservation.GuestName },
                    { "@email", reservation.GuestEmail },
                    { "@type", "OTA Guest" }
                };

                _code.DatabaseInsertSafe(_connectionString,
                    @"INSERT INTO Customer (Customer_MobilePhone, Customer_Name, Email, Customer_Type)
                      VALUES (@phone, @name, @email, @type)",
                    insertParams);
            }
        }

        private string MapOTAStatus(string otaStatus)
        {
            switch (otaStatus?.ToUpper())
            {
                case "CONFIRMED":
                case "BOOKED":
                    return "จองแล้ว";
                case "CANCELLED":
                    return "ยกเลิกคืนเงิน";
                case "CHECKED_IN":
                    return "เช็คอินแล้ว";
                case "CHECKED_OUT":
                case "COMPLETED":
                    return "เช็คเอาท์แล้ว";
                default:
                    return "จองแล้ว";
            }
        }

        #endregion

        #region API Communication

        /// <summary>
        /// Send data to channel API
        /// </summary>
        private async Task<ApiResponse> SendToChannelApi(ChannelConfig config, string endpoint, string payload)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{config.ApiEndpoint}/{endpoint}");
                request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
                request.Headers.Add("X-API-Secret", config.ApiSecret);
                request.Headers.Add("X-Hotel-ID", config.HotelId);
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                return new ApiResponse
                {
                    Success = response.IsSuccessStatusCode,
                    StatusCode = (int)response.StatusCode,
                    Message = response.IsSuccessStatusCode ? "Success" : content,
                    Data = content
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// Fetch data from channel API
        /// </summary>
        private async Task<ApiResponse> FetchFromChannelApi(ChannelConfig config, string endpoint)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{config.ApiEndpoint}/{endpoint}");
                request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
                request.Headers.Add("X-API-Secret", config.ApiSecret);
                request.Headers.Add("X-Hotel-ID", config.HotelId);

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                return new ApiResponse
                {
                    Success = response.IsSuccessStatusCode,
                    StatusCode = (int)response.StatusCode,
                    Data = content
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        #endregion

        #region Logging

        private void LogSyncResult(SyncResult result)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@startTime", result.StartTime },
                { "@endTime", result.EndTime },
                { "@success", result.Success },
                { "@details", JsonConvert.SerializeObject(result.ChannelResults) }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"INSERT INTO OTA_Sync_Log (Sync_Start, Sync_End, Is_Success, Details)
                  VALUES (@startTime, @endTime, @success, @details)",
                parameters);
        }

        #endregion

        #region Channel Management

        /// <summary>
        /// Get all configured channels
        /// </summary>
        public List<ChannelConfig> GetAllChannels()
        {
            return new List<ChannelConfig>(_channelConfigs.Values);
        }

        /// <summary>
        /// Update channel configuration
        /// </summary>
        public bool UpdateChannelConfig(ChannelConfig config)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@code", config.ChannelCode },
                    { "@name", config.ChannelName },
                    { "@endpoint", config.ApiEndpoint },
                    { "@apiKey", config.ApiKey },
                    { "@apiSecret", config.ApiSecret },
                    { "@hotelId", config.HotelId },
                    { "@isActive", config.IsActive },
                    { "@syncInterval", config.SyncIntervalMinutes }
                };

                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE OTA_Channels SET
                        Channel_Name = @name, API_Endpoint = @endpoint, API_Key = @apiKey,
                        API_Secret = @apiSecret, Hotel_ID = @hotelId, Is_Active = @isActive,
                        Sync_Interval_Minutes = @syncInterval, Modified_Date = GETDATE()
                      WHERE Channel_Code = @code",
                    parameters);

                // Reload configurations
                _channelConfigs[config.ChannelCode] = config;
                return true;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "ChannelManager", $"Update config failed: {ex.Message}", "SYSTEM");
                return false;
            }
        }

        #endregion
    }

    #region Data Models

    public class ChannelConfig
    {
        public string ChannelCode { get; set; }
        public string ChannelName { get; set; }
        public string ApiEndpoint { get; set; }
        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }
        public string HotelId { get; set; }
        public bool IsActive { get; set; }
        public int SyncIntervalMinutes { get; set; }
    }

    public class AvailabilityData
    {
        public int RoomTypeId { get; set; }
        public string RoomTypeName { get; set; }
        public DateTime Date { get; set; }
        public int Available { get; set; }
        public decimal Price { get; set; }
        public int Capacity { get; set; }
    }

    public class RateData
    {
        public int RoomTypeId { get; set; }
        public string RoomTypeName { get; set; }
        public DateTime Date { get; set; }
        public decimal BaseRate { get; set; }
        public decimal FinalRate { get; set; }
    }

    public class SyncResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool Success { get; set; }
        public List<ChannelSyncResult> ChannelResults { get; set; } = new List<ChannelSyncResult>();
    }

    public class ChannelSyncResult
    {
        public string ChannelCode { get; set; }
        public DateTime SyncTime { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public int RecordsSynced { get; set; }
    }

    public class ImportResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool Success { get; set; }
        public int ImportedCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class OTAReservation
    {
        public string ChannelBookingId { get; set; }
        public string GuestName { get; set; }
        public string GuestEmail { get; set; }
        public string GuestPhone { get; set; }
        public DateTime CheckInDate { get; set; }
        public DateTime CheckOutDate { get; set; }
        public int RoomTypeId { get; set; }
        public int RoomCount { get; set; }
        public int GuestCount { get; set; }
        public decimal TotalPrice { get; set; }
        public string Status { get; set; }
        public string SpecialRequests { get; set; }
    }

    public class ApiResponse
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; }
        public string Data { get; set; }
    }

    #endregion
}
