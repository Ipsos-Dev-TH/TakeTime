using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Services
{
    public class AIKnowledgeService
    {
        private readonly string _connStr;
        private readonly code _code;

        public AIKnowledgeService(string connectionString)
        {
            _connStr = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _code = new code();
        }

        #region Feature Settings

        public bool IsFeatureEnabled(string featureCode)
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connStr,
                    "SELECT IsEnabled FROM AI_Feature_Settings WHERE FeatureCode = @Code",
                    new Dictionary<string, object> { { "@Code", featureCode } });
                return dt.Rows.Count > 0 && Convert.ToBoolean(dt.Rows[0]["IsEnabled"]);
            }
            catch { return false; }
        }

        public DataTable GetAllFeatures()
        {
            return _code.DatabaseQuerySafe(_connStr,
                "SELECT * FROM AI_Feature_Settings ORDER BY ID", null);
        }

        public void SetFeatureEnabled(string featureCode, bool enabled)
        {
            _code.DatabaseInsertSafe(_connStr,
                "UPDATE AI_Feature_Settings SET IsEnabled = @Enabled, Updated_Date = GETDATE() WHERE FeatureCode = @Code",
                new Dictionary<string, object> { { "@Code", featureCode }, { "@Enabled", enabled } });
        }

        #endregion

        #region Knowledge Base

        public DataTable GetKnowledgeBase(string category = null, string search = null)
        {
            string sql = "SELECT * FROM AI_Knowledge_Base WHERE IsActive = 1";
            var parms = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(category))
            {
                sql += " AND Category = @Cat";
                parms["@Cat"] = category;
            }
            if (!string.IsNullOrEmpty(search))
            {
                sql += " AND (Question LIKE @S OR Answer LIKE @S OR Keywords LIKE @S)";
                parms["@S"] = "%" + search + "%";
            }
            sql += " ORDER BY MatchCount DESC, Created_Date DESC";

            return _code.DatabaseQuerySafe(_connStr, sql, parms);
        }

        public void AddKnowledgeEntry(string category, string question, string answer, string keywords, string source = "MANUAL")
        {
            _code.DatabaseInsertSafe(_connStr,
                @"INSERT INTO AI_Knowledge_Base (Category, Question, Answer, Keywords, Source, Created_Date)
                  VALUES (@Cat, @Q, @A, @K, @Src, GETDATE())",
                new Dictionary<string, object>
                {
                    { "@Cat", category }, { "@Q", question }, { "@A", answer },
                    { "@K", keywords }, { "@Src", source }
                });
        }

        public void UpdateKnowledgeEntry(long id, string question, string answer, string keywords, string category)
        {
            _code.DatabaseInsertSafe(_connStr,
                "UPDATE AI_Knowledge_Base SET Question = @Q, Answer = @A, Keywords = @K, Category = @Cat, Updated_Date = GETDATE() WHERE ID = @Id",
                new Dictionary<string, object>
                {
                    { "@Id", id }, { "@Q", question }, { "@A", answer },
                    { "@K", keywords }, { "@Cat", category }
                });
        }

        public void DeleteKnowledgeEntry(long id)
        {
            _code.DatabaseInsertSafe(_connStr,
                "UPDATE AI_Knowledge_Base SET IsActive = 0 WHERE ID = @Id",
                new Dictionary<string, object> { { "@Id", id } });
        }

        public int ImportKnowledgeFromText(string text, string source = "UPLOAD")
        {
            int count = 0;
            var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length - 1; i += 2)
            {
                string q = lines[i].Trim().TrimStart('Q', 'q', ':', '：', ' ');
                string a = (i + 1 < lines.Length) ? lines[i + 1].Trim().TrimStart('A', 'a', ':', '：', ' ') : "";
                if (string.IsNullOrEmpty(q) || string.IsNullOrEmpty(a)) continue;

                string keywords = ExtractKeywords(q);
                AddKnowledgeEntry("GENERAL", q, a, keywords, source);
                count++;
            }
            return count;
        }

        public int ImportKnowledgeCsv(string csvContent, string source = "CSV_UPLOAD")
        {
            int count = 0;
            var lines = csvContent.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 1; i < lines.Length; i++)
            {
                var parts = ParseCsvLine(lines[i]);
                if (parts.Count < 2) continue;

                string category = parts.Count >= 3 ? parts[0].Trim() : "GENERAL";
                string question = parts.Count >= 3 ? parts[1].Trim() : parts[0].Trim();
                string answer = parts.Count >= 3 ? parts[2].Trim() : parts[1].Trim();
                string keywords = parts.Count >= 4 ? parts[3].Trim() : ExtractKeywords(question);

                if (string.IsNullOrEmpty(question) || string.IsNullOrEmpty(answer)) continue;

                AddKnowledgeEntry(category, question, answer, keywords, source);
                count++;
            }
            return count;
        }

        #endregion

        #region Local Pattern Matching (learns during usage)

        public KBMatchResult FindBestMatch(string userInput)
        {
            string normalized = NormalizeText(userInput);
            string[] inputWords = TokenizeText(normalized);
            if (inputWords.Length == 0) return null;

            var bestMatch = new KBMatchResult();
            double bestScore = 0;

            DataTable dtKB = _code.DatabaseQuerySafe(_connStr,
                "SELECT ID, Question, Answer, Keywords, Category FROM AI_Knowledge_Base WHERE IsActive = 1", null);

            foreach (DataRow row in dtKB.Rows)
            {
                string keywords = row["Keywords"]?.ToString() ?? "";
                string question = row["Question"]?.ToString() ?? "";

                double score = CalculateMatchScore(inputWords, normalized, keywords, question);
                if (score > bestScore && score >= 0.3)
                {
                    bestScore = score;
                    bestMatch = new KBMatchResult
                    {
                        MatchId = Convert.ToInt64(row["ID"]),
                        Answer = row["Answer"].ToString(),
                        Category = row["Category"].ToString(),
                        Confidence = score,
                        Source = "KNOWLEDGE_BASE"
                    };
                }
            }

            DataTable dtPatterns = _code.DatabaseQuerySafe(_connStr,
                "SELECT ID, InputPattern, InputKeywords, Response, Category, Confidence FROM AI_Learned_Patterns WHERE IsActive = 1", null);

            foreach (DataRow row in dtPatterns.Rows)
            {
                string keywords = row["InputKeywords"]?.ToString() ?? "";
                string pattern = row["InputPattern"]?.ToString() ?? "";
                double baseConf = row["Confidence"] != DBNull.Value ? Convert.ToDouble(row["Confidence"]) : 0.5;

                double score = CalculateMatchScore(inputWords, normalized, keywords, pattern) * baseConf;
                if (score > bestScore && score >= 0.25)
                {
                    bestScore = score;
                    bestMatch = new KBMatchResult
                    {
                        MatchId = Convert.ToInt64(row["ID"]),
                        Answer = row["Response"].ToString(),
                        Category = row["Category"]?.ToString(),
                        Confidence = score,
                        Source = "LEARNED_PATTERN"
                    };
                }
            }

            if (bestScore >= 0.3)
            {
                try
                {
                    if (bestMatch.Source == "KNOWLEDGE_BASE")
                        _code.DatabaseInsertSafe(_connStr, "UPDATE AI_Knowledge_Base SET MatchCount = MatchCount + 1 WHERE ID = @Id",
                            new Dictionary<string, object> { { "@Id", bestMatch.MatchId } });
                    else
                        _code.DatabaseInsertSafe(_connStr, "UPDATE AI_Learned_Patterns SET SuccessCount = SuccessCount + 1 WHERE ID = @Id",
                            new Dictionary<string, object> { { "@Id", bestMatch.MatchId } });
                }
                catch { }

                return bestMatch;
            }

            return null;
        }

        private double CalculateMatchScore(string[] inputWords, string normalizedInput, string keywords, string reference)
        {
            double score = 0;
            int matchedKeywords = 0;

            if (!string.IsNullOrEmpty(keywords))
            {
                var kwList = keywords.Split(new[] { ',', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim().ToLower()).ToArray();

                foreach (string kw in kwList)
                {
                    if (normalizedInput.Contains(kw))
                        matchedKeywords++;
                }

                if (kwList.Length > 0)
                    score = (double)matchedKeywords / kwList.Length;
            }

            if (!string.IsNullOrEmpty(reference))
            {
                string normRef = NormalizeText(reference);
                string[] refWords = TokenizeText(normRef);
                int commonWords = inputWords.Count(w => refWords.Contains(w));
                double wordOverlap = refWords.Length > 0 ? (double)commonWords / Math.Max(refWords.Length, inputWords.Length) : 0;
                score = Math.Max(score, wordOverlap);
            }

            return score;
        }

        #endregion

        #region Learning from Staff

        public void LearnFromStaffReply(string customerMessage, string staffReply, string category = null, string staffName = null)
        {
            if (string.IsNullOrEmpty(customerMessage) || string.IsNullOrEmpty(staffReply)) return;

            try
            {
                string keywords = ExtractKeywords(customerMessage);

                DataTable existing = _code.DatabaseQuerySafe(_connStr,
                    "SELECT ID, SuccessCount FROM AI_Learned_Patterns WHERE InputKeywords = @K AND IsActive = 1",
                    new Dictionary<string, object> { { "@K", keywords } });

                if (existing.Rows.Count > 0)
                {
                    _code.DatabaseInsertSafe(_connStr,
                        "UPDATE AI_Learned_Patterns SET Response = @R, SuccessCount = SuccessCount + 1, Confidence = CASE WHEN Confidence < 0.95 THEN Confidence + 0.05 ELSE 0.95 END, Updated_Date = GETDATE() WHERE ID = @Id",
                        new Dictionary<string, object> { { "@Id", existing.Rows[0]["ID"] }, { "@R", staffReply } });
                }
                else
                {
                    _code.DatabaseInsertSafe(_connStr,
                        @"INSERT INTO AI_Learned_Patterns (InputPattern, InputKeywords, Response, Category, Confidence, Source, LearnedFrom, Created_Date)
                          VALUES (@P, @K, @R, @Cat, 0.5, 'STAFF_REPLY', @Staff, GETDATE())",
                        new Dictionary<string, object>
                        {
                            { "@P", customerMessage },
                            { "@K", keywords },
                            { "@R", staffReply },
                            { "@Cat", category ?? "GENERAL" },
                            { "@Staff", staffName ?? "Unknown" }
                        });
                }
            }
            catch { }
        }

        public DataTable GetLearnedPatterns(int limit = 100)
        {
            return _code.DatabaseQuerySafe(_connStr,
                "SELECT TOP (@Limit) * FROM AI_Learned_Patterns WHERE IsActive = 1 ORDER BY Confidence DESC, SuccessCount DESC",
                new Dictionary<string, object> { { "@Limit", limit } });
        }

        public void DeleteLearnedPattern(long id)
        {
            _code.DatabaseInsertSafe(_connStr,
                "UPDATE AI_Learned_Patterns SET IsActive = 0 WHERE ID = @Id",
                new Dictionary<string, object> { { "@Id", id } });
        }

        public Dictionary<string, object> GetLearningStats()
        {
            var stats = new Dictionary<string, object>();
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT
                        (SELECT COUNT(*) FROM AI_Knowledge_Base WHERE IsActive = 1) AS KBCount,
                        (SELECT COUNT(*) FROM AI_Learned_Patterns WHERE IsActive = 1) AS PatternCount,
                        (SELECT ISNULL(SUM(MatchCount), 0) FROM AI_Knowledge_Base) AS KBMatches,
                        (SELECT ISNULL(SUM(SuccessCount), 0) FROM AI_Learned_Patterns) AS PatternMatches,
                        (SELECT ISNULL(AVG(Confidence), 0) FROM AI_Learned_Patterns WHERE IsActive = 1) AS AvgConfidence", null);

                if (dt.Rows.Count > 0)
                {
                    stats["kbCount"] = Convert.ToInt32(dt.Rows[0]["KBCount"]);
                    stats["patternCount"] = Convert.ToInt32(dt.Rows[0]["PatternCount"]);
                    stats["kbMatches"] = Convert.ToInt32(dt.Rows[0]["KBMatches"]);
                    stats["patternMatches"] = Convert.ToInt32(dt.Rows[0]["PatternMatches"]);
                    stats["avgConfidence"] = Math.Round(Convert.ToDouble(dt.Rows[0]["AvgConfidence"]), 2);
                }
            }
            catch { }
            return stats;
        }

        #endregion

        #region Booking Integration

        public string GetBookingContext(string customerPhone = null, string query = null)
        {
            var context = new System.Text.StringBuilder();

            try
            {
                DataTable dtRooms = _code.DatabaseQuerySafe(_connStr,
                    "SELECT AccomName, Price, People, LimitWithPeople FROM Accommodation WHERE Status = 1 ORDER BY OrderID", null);

                context.AppendLine("=== ข้อมูลห้องพัก ===");
                foreach (DataRow r in dtRooms.Rows)
                {
                    string priceType = r["LimitWithPeople"]?.ToString() == "True" ? "/คน/คืน" : "/คืน";
                    context.AppendLine($"- {r["AccomName"]}: ราคา {Convert.ToDecimal(r["Price"]):N0} บาท{priceType} (รองรับ {r["People"]} คน)");
                }

                if (!string.IsNullOrEmpty(customerPhone))
                {
                    DataTable dtRes = _code.DatabaseQuerySafe(_connStr,
                        @"SELECT TOP 5 r.ID, r.CheckinDate, r.CheckoutDate, r.Status, r.TotalPrice,
                                 ISNULL(dbo.fn_GetReservationRoomNames(r.ID), N'') AS RoomNames
                          FROM Reservation r WHERE r.Customer_MobilePhone = @Phone
                          ORDER BY r.Created_Date DESC",
                        new Dictionary<string, object> { { "@Phone", customerPhone } });

                    if (dtRes.Rows.Count > 0)
                    {
                        context.AppendLine($"\n=== การจองของลูกค้า {customerPhone} ===");
                        foreach (DataRow r in dtRes.Rows)
                        {
                            context.AppendLine($"- #{r["ID"]} | {r["RoomNames"]} | {Convert.ToDateTime(r["CheckinDate"]):dd/MM/yyyy} - {Convert.ToDateTime(r["CheckoutDate"]):dd/MM/yyyy} | {r["Status"]} | {Convert.ToDecimal(r["TotalPrice"]):N0} บาท");
                        }
                    }
                }

                if (query != null && (query.Contains("ว่าง") || query.Contains("จอง") || query.Contains("available") || query.Contains("ราคา")))
                {
                    DateTime checkDate = DateTime.Today;
                    context.AppendLine($"\n=== ห้องว่างวันนี้ ({checkDate:dd/MM/yyyy}) ===");

                    foreach (DataRow room in dtRooms.Rows)
                    {
                        int accomId = Convert.ToInt32(room["ID"] ?? 0);
                        bool isLimitPeople = room["LimitWithPeople"]?.ToString() == "True";

                        DataTable dtConflict = _code.DatabaseQuerySafe(_connStr,
                            @"SELECT ISNULL(SUM(ra.Amount), 0) AS Booked
                              FROM Reservation_Accommodation ra
                              JOIN Reservation r ON r.ID = ra.Reservation_ID
                              WHERE ra.Accommodation_ID = @AccId
                                AND r.CheckinDate <= @Date AND r.CheckoutDate > @Date
                                AND r.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')",
                            new Dictionary<string, object> { { "@AccId", accomId }, { "@Date", checkDate } });

                        int booked = dtConflict.Rows.Count > 0 ? Convert.ToInt32(dtConflict.Rows[0]["Booked"]) : 0;
                        int capacity = Convert.ToInt32(room["People"]);

                        if (isLimitPeople)
                            context.AppendLine($"- {room["AccomName"]}: ว่าง {capacity - booked}/{capacity} คน");
                        else
                            context.AppendLine($"- {room["AccomName"]}: {(booked == 0 ? "ว่าง" : "ไม่ว่าง")}");
                    }
                }
            }
            catch (Exception ex)
            {
                context.AppendLine("(ไม่สามารถดึงข้อมูลระบบได้: " + ex.Message + ")");
            }

            return context.ToString();
        }

        public Dictionary<string, object> CheckAvailability(DateTime checkIn, DateTime checkOut)
        {
            var result = new Dictionary<string, object>();
            var available = new List<Dictionary<string, object>>();

            try
            {
                int nights = (checkOut - checkIn).Days;
                if (nights <= 0) nights = 1;

                DataTable dtRooms = _code.DatabaseQuerySafe(_connStr,
                    "SELECT ID, AccomName, Price, People, LimitWithPeople FROM Accommodation WHERE Status = 1 ORDER BY OrderID", null);

                foreach (DataRow room in dtRooms.Rows)
                {
                    int accomId = Convert.ToInt32(room["ID"]);
                    bool isLimitPeople = room["LimitWithPeople"]?.ToString() == "True";
                    int capacity = Convert.ToInt32(room["People"]);
                    decimal price = Convert.ToDecimal(room["Price"]);

                    DataTable dtConflict = _code.DatabaseQuerySafe(_connStr,
                        @"SELECT ISNULL(SUM(ra.Amount), 0) AS Booked
                          FROM Reservation_Accommodation ra
                          JOIN Reservation r ON r.ID = ra.Reservation_ID
                          WHERE ra.Accommodation_ID = @AccId
                            AND r.CheckinDate < @Out AND r.CheckoutDate > @In
                            AND r.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')",
                        new Dictionary<string, object> { { "@AccId", accomId }, { "@In", checkIn }, { "@Out", checkOut } });

                    int booked = dtConflict.Rows.Count > 0 ? Convert.ToInt32(dtConflict.Rows[0]["Booked"]) : 0;
                    int availableSlots = isLimitPeople ? capacity - booked : (booked == 0 ? 1 : 0);

                    if (availableSlots > 0)
                    {
                        available.Add(new Dictionary<string, object>
                        {
                            { "id", accomId },
                            { "name", room["AccomName"].ToString() },
                            { "price", price },
                            { "priceType", isLimitPeople ? "per_person" : "per_room" },
                            { "available", availableSlots },
                            { "capacity", capacity },
                            { "totalPrice", isLimitPeople ? price * nights : price * nights }
                        });
                    }
                }

                result["success"] = true;
                result["checkIn"] = checkIn.ToString("dd/MM/yyyy");
                result["checkOut"] = checkOut.ToString("dd/MM/yyyy");
                result["nights"] = nights;
                result["rooms"] = available;
            }
            catch (Exception ex)
            {
                result["success"] = false;
                result["error"] = ex.Message;
            }

            return result;
        }

        public Dictionary<string, object> CreatePendingBooking(string phone, string name, int accomId, DateTime checkIn, DateTime checkOut, int people, long conversationId)
        {
            try
            {
                int nights = (checkOut - checkIn).Days;
                if (nights <= 0) return new Dictionary<string, object> { { "success", false }, { "error", "วันที่ไม่ถูกต้อง" } };

                DataTable dtRoom = _code.DatabaseQuerySafe(_connStr,
                    "SELECT AccomName, Price, People, LimitWithPeople FROM Accommodation WHERE ID = @Id AND Status = 1",
                    new Dictionary<string, object> { { "@Id", accomId } });

                if (dtRoom.Rows.Count == 0)
                    return new Dictionary<string, object> { { "success", false }, { "error", "ไม่พบห้องพัก" } };

                bool isLimitPeople = dtRoom.Rows[0]["LimitWithPeople"]?.ToString() == "True";
                decimal price = Convert.ToDecimal(dtRoom.Rows[0]["Price"]);
                decimal total = isLimitPeople ? price * people * nights : price * nights;

                _code.DatabaseInsertSafe(_connStr,
                    @"IF NOT EXISTS (SELECT 1 FROM Customer WHERE MobilePhone = @Phone)
                      INSERT INTO Customer (MobilePhone, Name) VALUES (@Phone, @Name)",
                    new Dictionary<string, object> { { "@Phone", phone }, { "@Name", name } });

                int resId = _code.DatabaseInsertReturnSafe(_connStr,
                    @"INSERT INTO Reservation (Customer_MobilePhone, CheckinDate, CheckoutDate, StayDays, TotalPrice, Deposit, Status, Remark, Reserve_By, Created_Date)
                      VALUES (@Phone, @In, @Out, @Nights, @Total, 0, N'รอยืนยัน', N'จองผ่าน AI - รอพนักงานยืนยัน', N'AI Assistant', GETDATE());
                      SELECT SCOPE_IDENTITY();",
                    new Dictionary<string, object>
                    {
                        { "@Phone", phone }, { "@In", checkIn }, { "@Out", checkOut },
                        { "@Nights", nights }, { "@Total", total }
                    });

                _code.DatabaseInsertSafe(_connStr,
                    "INSERT INTO Reservation_Accommodation (Reservation_ID, Accommodation_ID, Amount, Price) VALUES (@ResId, @AccId, @Amt, @Price)",
                    new Dictionary<string, object>
                    {
                        { "@ResId", resId }, { "@AccId", accomId },
                        { "@Amt", isLimitPeople ? people : 1 }, { "@Price", price }
                    });

                _code.DatabaseInsertSafe(_connStr,
                    @"INSERT INTO AI_Booking_Actions (ConversationID, ActionType, RequestData, ResponseData, ReservationID, Status, Created_Date)
                      VALUES (@ConvId, 'CREATE', @Req, @Resp, @ResId, 'PENDING', GETDATE())",
                    new Dictionary<string, object>
                    {
                        { "@ConvId", conversationId },
                        { "@Req", $"Phone:{phone}, Room:{accomId}, {checkIn:dd/MM}-{checkOut:dd/MM}, {people}pax" },
                        { "@Resp", $"Reservation #{resId}, Total: {total:N0}" },
                        { "@ResId", resId }
                    });

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "reservationId", resId },
                    { "roomName", dtRoom.Rows[0]["AccomName"].ToString() },
                    { "checkIn", checkIn.ToString("dd/MM/yyyy") },
                    { "checkOut", checkOut.ToString("dd/MM/yyyy") },
                    { "nights", nights },
                    { "total", total },
                    { "status", "รอยืนยัน" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "error", ex.Message } };
            }
        }

        #endregion

        #region Auto Reply Engine

        public AutoReplyResult ProcessAutoReply(string userMessage, string channelCode = null, string customerPhone = null, long conversationId = 0)
        {
            bool kbOnly = IsFeatureEnabled("AUTO_REPLY_KB_ONLY");

            var kbMatch = FindBestMatch(userMessage);
            if (kbMatch != null && kbMatch.Confidence >= 0.5)
            {
                return new AutoReplyResult
                {
                    Success = true,
                    Reply = kbMatch.Answer,
                    Confidence = kbMatch.Confidence,
                    Source = kbMatch.Source,
                    Category = kbMatch.Category
                };
            }

            if (kbOnly)
                return new AutoReplyResult { Success = false, Reason = "NO_KB_MATCH" };

            try
            {
                var deepSeek = new DeepSeekService(_connStr);
                if (!deepSeek.IsEnabled || !deepSeek.HasApiKey())
                    return new AutoReplyResult { Success = false, Reason = "AI_NOT_CONFIGURED" };

                string systemContext = "";
                if (IsFeatureEnabled("BOOKING_LOOKUP"))
                    systemContext = "\n\n" + GetBookingContext(customerPhone, userMessage);

                string kbContext = BuildKBContext(userMessage);
                if (!string.IsNullOrEmpty(kbContext))
                    systemContext += "\n\n=== ข้อมูลที่เกี่ยวข้อง ===\n" + kbContext;

                string originalPrompt = deepSeek.GetAllConfig()
                    .ContainsKey("AI_SystemPrompt") ? deepSeek.GetAllConfig()["AI_SystemPrompt"] : "";

                string enhancedPrompt = originalPrompt + systemContext;
                if (IsFeatureEnabled("BOOKING_CREATE"))
                    enhancedPrompt += "\nหากลูกค้าต้องการจองห้องพัก ให้สอบถามวันเช็คอิน เช็คเอาท์ จำนวนผู้เข้าพัก และเบอร์โทร แล้วสรุปข้อมูลให้";

                deepSeek.SetConfig("AI_SystemPrompt", enhancedPrompt);

                string sessionKey = "omni_" + conversationId + "_" + channelCode;
                var history = deepSeek.GetHistory(sessionKey);
                var result = deepSeek.SendMessage(userMessage, sessionKey, history);

                deepSeek.SetConfig("AI_SystemPrompt", originalPrompt);

                if (result.Success)
                {
                    return new AutoReplyResult
                    {
                        Success = true,
                        Reply = result.Message,
                        Confidence = 0.7,
                        Source = "DEEPSEEK_API",
                        TokensUsed = result.TotalTokens
                    };
                }

                return new AutoReplyResult { Success = false, Reason = "API_ERROR", ErrorMessage = result.Message };
            }
            catch (Exception ex)
            {
                return new AutoReplyResult { Success = false, Reason = "ERROR", ErrorMessage = ex.Message };
            }
        }

        private string BuildKBContext(string query)
        {
            try
            {
                string normalized = NormalizeText(query);
                DataTable dtKB = _code.DatabaseQuerySafe(_connStr,
                    "SELECT TOP 5 Question, Answer FROM AI_Knowledge_Base WHERE IsActive = 1", null);

                var matches = new List<string>();
                foreach (DataRow row in dtKB.Rows)
                {
                    string keywords = "";
                    try
                    {
                        DataTable dtK = _code.DatabaseQuerySafe(_connStr,
                            "SELECT Keywords FROM AI_Knowledge_Base WHERE ID = " + row["ID"], null);
                        if (dtK.Rows.Count > 0) keywords = dtK.Rows[0]["Keywords"]?.ToString() ?? "";
                    }
                    catch { }

                    if (!string.IsNullOrEmpty(keywords))
                    {
                        var kwList = keywords.Split(',').Select(k => k.Trim().ToLower());
                        if (kwList.Any(k => normalized.Contains(k)))
                            matches.Add($"Q: {row["Question"]}\nA: {row["Answer"]}");
                    }
                }

                return string.Join("\n\n", matches);
            }
            catch { return ""; }
        }

        #endregion

        #region Text Processing Helpers

        private string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.ToLower().Trim();
            text = Regex.Replace(text, @"[^฀-๿a-z0-9\s]", " ");
            text = Regex.Replace(text, @"\s+", " ");
            return text;
        }

        private string[] TokenizeText(string text)
        {
            return text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 1).ToArray();
        }

        public string ExtractKeywords(string text)
        {
            string[] stopWords = { "ครับ", "ค่ะ", "คะ", "นะ", "ได้", "ไหม", "มี", "ที่", "จะ", "อยาก", "ต้อง", "การ", "ของ", "ให้", "แล้ว", "กรุณา", "ช่วย", "สอบถาม" };
            string normalized = NormalizeText(text);
            var words = TokenizeText(normalized)
                .Where(w => !stopWords.Contains(w) && w.Length >= 2)
                .Distinct()
                .Take(10);
            return string.Join(",", words);
        }

        private List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            foreach (char c in line)
            {
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); continue; }
                current.Append(c);
            }
            result.Add(current.ToString());
            return result;
        }

        #endregion
    }

    public class KBMatchResult
    {
        public long MatchId { get; set; }
        public string Answer { get; set; }
        public string Category { get; set; }
        public double Confidence { get; set; }
        public string Source { get; set; }
    }

    public class AutoReplyResult
    {
        public bool Success { get; set; }
        public string Reply { get; set; }
        public double Confidence { get; set; }
        public string Source { get; set; }
        public string Category { get; set; }
        public int TokensUsed { get; set; }
        public string Reason { get; set; }
        public string ErrorMessage { get; set; }
    }
}
