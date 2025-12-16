// ===========================================================================
// ReviewService.cs
// Guest Review Management Service
// Handles review submissions, approvals, responses, and analytics
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Data;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// Service for managing guest reviews and ratings
    /// </summary>
    public class ReviewService
    {
        private readonly string _connectionString;
        private readonly code _code;

        public ReviewService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _code = new code();
        }

        #region Review Submission

        /// <summary>
        /// Submit a new guest review
        /// </summary>
        public ReviewResult SubmitReview(ReviewSubmission review)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ReservationID", review.ReservationId },
                    { "@CustomerPhone", review.CustomerPhone },
                    { "@OverallRating", review.OverallRating },
                    { "@CleanlinessRating", review.CleanlinessRating ?? (object)DBNull.Value },
                    { "@ServiceRating", review.ServiceRating ?? (object)DBNull.Value },
                    { "@FacilitiesRating", review.FacilitiesRating ?? (object)DBNull.Value },
                    { "@LocationRating", review.LocationRating ?? (object)DBNull.Value },
                    { "@ValueForMoneyRating", review.ValueForMoneyRating ?? (object)DBNull.Value },
                    { "@ReviewTitle", review.ReviewTitle ?? (object)DBNull.Value },
                    { "@ReviewText", review.ReviewText ?? (object)DBNull.Value },
                    { "@Pros", review.Pros ?? (object)DBNull.Value },
                    { "@Cons", review.Cons ?? (object)DBNull.Value },
                    { "@WouldRecommend", review.WouldRecommend ?? (object)DBNull.Value },
                    { "@WouldReturn", review.WouldReturn ?? (object)DBNull.Value },
                    { "@TravelType", review.TravelType ?? (object)DBNull.Value },
                    { "@Source", review.Source ?? "DIRECT" }
                };

                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"EXEC sp_SubmitGuestReview @ReservationID, @CustomerPhone, @OverallRating,
                      @CleanlinessRating, @ServiceRating, @FacilitiesRating, @LocationRating,
                      @ValueForMoneyRating, @ReviewTitle, @ReviewText, @Pros, @Cons,
                      @WouldRecommend, @WouldReturn, @TravelType, @Source",
                    parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    string result = row["Result"].ToString();

                    if (result == "SUCCESS")
                    {
                        return new ReviewResult
                        {
                            Success = true,
                            Message = "Review submitted successfully",
                            ReviewId = row["ReviewID"] != DBNull.Value ? Convert.ToInt64(row["ReviewID"]) : 0
                        };
                    }
                    else if (result == "ALREADY_REVIEWED")
                    {
                        return new ReviewResult
                        {
                            Success = false,
                            Message = "You have already reviewed this reservation"
                        };
                    }
                }

                return new ReviewResult { Success = false, Message = "Unknown error" };
            }
            catch (Exception ex)
            {
                return new ReviewResult { Success = false, Message = "Error: " + ex.Message };
            }
        }

        /// <summary>
        /// Check if customer can review a reservation
        /// </summary>
        public bool CanReview(long reservationId, string customerPhone)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ReservationID", reservationId },
                    { "@CustomerPhone", customerPhone }
                };

                // Check if reservation exists and belongs to customer
                DataTable dtReservation = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT Status, CheckoutDate
                      FROM Reservation
                      WHERE ID = @ReservationID AND Customer_MobilePhone = @CustomerPhone",
                    parameters);

                if (dtReservation.Rows.Count == 0)
                    return false; // Not found or not owned by customer

                string status = dtReservation.Rows[0]["Status"].ToString();
                if (status != "เช็คเอาท์แล้ว")
                    return false; // Not checked out yet

                // Check if already reviewed
                DataTable dtReview = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT COUNT(*) FROM Guest_Reviews WHERE Reservation_ID = @ReservationID",
                    parameters);

                if (dtReview.Rows.Count > 0 && Convert.ToInt32(dtReview.Rows[0][0]) > 0)
                    return false; // Already reviewed

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Review Management

        /// <summary>
        /// Get reviews pending approval
        /// </summary>
        public DataTable GetPendingReviews()
        {
            try
            {
                return _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT GR.*, C.Name AS CustomerName, R.ID AS ReservationNumber
                      FROM Guest_Reviews GR
                      INNER JOIN Customer C ON C.MobilePhone = GR.Customer_MobilePhone
                      INNER JOIN Reservation R ON R.ID = GR.Reservation_ID
                      WHERE GR.Status = 'PENDING'
                      ORDER BY GR.SubmittedDate DESC",
                    null);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting pending reviews: " + ex.Message);
            }
        }

        /// <summary>
        /// Approve a review
        /// </summary>
        public bool ApproveReview(long reviewId, short adminId)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ReviewID", reviewId },
                    { "@AdminID", adminId }
                };

                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Guest_Reviews
                      SET Status = 'APPROVED',
                          IsPublic = 1,
                          ApprovedDate = GETDATE(),
                          ApprovedBy_AdminID = @AdminID
                      WHERE ID = @ReviewID",
                    parameters);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reject a review
        /// </summary>
        public bool RejectReview(long reviewId, short adminId)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ReviewID", reviewId },
                    { "@AdminID", adminId }
                };

                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Guest_Reviews
                      SET Status = 'REJECTED',
                          IsPublic = 0,
                          ApprovedDate = GETDATE(),
                          ApprovedBy_AdminID = @AdminID
                      WHERE ID = @ReviewID",
                    parameters);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Add management response to review
        /// </summary>
        public bool AddResponse(long reviewId, string responseText, short adminId)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ReviewID", reviewId },
                    { "@ResponseText", responseText },
                    { "@AdminID", adminId }
                };

                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Guest_Reviews
                      SET ResponseText = @ResponseText,
                          ResponseDate = GETDATE(),
                          ResponseBy_AdminID = @AdminID
                      WHERE ID = @ReviewID",
                    parameters);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Toggle review featured status
        /// </summary>
        public bool ToggleFeatured(long reviewId)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ReviewID", reviewId }
                };

                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Guest_Reviews
                      SET IsFeatured = CASE WHEN IsFeatured = 1 THEN 0 ELSE 1 END
                      WHERE ID = @ReviewID",
                    parameters);

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Review Queries

        /// <summary>
        /// Get all reviews with filters
        /// </summary>
        public DataTable GetReviews(string status = null, int? minRating = null, int limit = 50)
        {
            try
            {
                var parameters = new Dictionary<string, object>();
                string query = @"
                    SELECT GR.*, C.Name AS CustomerName, R.ID AS ReservationNumber,
                           A.FirstName + ' ' + A.LastName AS ApprovedByName
                    FROM Guest_Reviews GR
                    INNER JOIN Customer C ON C.MobilePhone = GR.Customer_MobilePhone
                    INNER JOIN Reservation R ON R.ID = GR.Reservation_ID
                    LEFT JOIN Admin A ON A.ID = GR.ApprovedBy_AdminID
                    WHERE 1=1";

                if (!string.IsNullOrEmpty(status))
                {
                    query += " AND GR.Status = @Status";
                    parameters["@Status"] = status;
                }

                if (minRating.HasValue)
                {
                    query += " AND GR.OverallRating >= @MinRating";
                    parameters["@MinRating"] = minRating.Value;
                }

                query += code.AdaptSql($" ORDER BY GR.SubmittedDate DESC LIMIT {limit}");

                return _code.DatabaseQuerySafe(_connectionString, query, parameters);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting reviews: " + ex.Message);
            }
        }

        /// <summary>
        /// Get reviews for a specific customer
        /// </summary>
        public DataTable GetCustomerReviews(string customerPhone)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@CustomerPhone", customerPhone }
                };

                return _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT GR.*, R.ID AS ReservationNumber, R.CheckinDate, R.CheckoutDate
                      FROM Guest_Reviews GR
                      INNER JOIN Reservation R ON R.ID = GR.Reservation_ID
                      WHERE GR.Customer_MobilePhone = @CustomerPhone
                      ORDER BY GR.SubmittedDate DESC",
                    parameters);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting customer reviews: " + ex.Message);
            }
        }

        /// <summary>
        /// Get review by ID
        /// </summary>
        public DataRow GetReviewById(long reviewId)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ReviewID", reviewId }
                };

                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT GR.*, C.Name AS CustomerName, C.Email,
                             R.ID AS ReservationNumber, R.CheckinDate, R.CheckoutDate
                      FROM Guest_Reviews GR
                      INNER JOIN Customer C ON C.MobilePhone = GR.Customer_MobilePhone
                      INNER JOIN Reservation R ON R.ID = GR.Reservation_ID
                      WHERE GR.ID = @ReviewID",
                    parameters);

                return dt.Rows.Count > 0 ? dt.Rows[0] : null;
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting review: " + ex.Message);
            }
        }

        #endregion

        #region Analytics

        /// <summary>
        /// Get review analytics summary
        /// </summary>
        public ReviewAnalytics GetAnalytics()
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT * FROM vw_Review_Analytics",
                    null);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    return new ReviewAnalytics
                    {
                        TotalReviews = row["TotalReviews"] != DBNull.Value ? Convert.ToInt32(row["TotalReviews"]) : 0,
                        ApprovedReviews = row["ApprovedReviews"] != DBNull.Value ? Convert.ToInt32(row["ApprovedReviews"]) : 0,
                        PendingReviews = row["PendingReviews"] != DBNull.Value ? Convert.ToInt32(row["PendingReviews"]) : 0,
                        AvgOverallRating = row["AvgOverallRating"] != DBNull.Value ? Convert.ToDouble(row["AvgOverallRating"]) : 0,
                        AvgCleanlinessRating = row["AvgCleanlinessRating"] != DBNull.Value ? Convert.ToDouble(row["AvgCleanlinessRating"]) : 0,
                        AvgServiceRating = row["AvgServiceRating"] != DBNull.Value ? Convert.ToDouble(row["AvgServiceRating"]) : 0,
                        AvgFacilitiesRating = row["AvgFacilitiesRating"] != DBNull.Value ? Convert.ToDouble(row["AvgFacilitiesRating"]) : 0,
                        AvgLocationRating = row["AvgLocationRating"] != DBNull.Value ? Convert.ToDouble(row["AvgLocationRating"]) : 0,
                        AvgValueForMoneyRating = row["AvgValueForMoneyRating"] != DBNull.Value ? Convert.ToDouble(row["AvgValueForMoneyRating"]) : 0,
                        FiveStarCount = row["FiveStarCount"] != DBNull.Value ? Convert.ToInt32(row["FiveStarCount"]) : 0,
                        FourStarCount = row["FourStarCount"] != DBNull.Value ? Convert.ToInt32(row["FourStarCount"]) : 0,
                        ThreeStarCount = row["ThreeStarCount"] != DBNull.Value ? Convert.ToInt32(row["ThreeStarCount"]) : 0,
                        TwoStarCount = row["TwoStarCount"] != DBNull.Value ? Convert.ToInt32(row["TwoStarCount"]) : 0,
                        OneStarCount = row["OneStarCount"] != DBNull.Value ? Convert.ToInt32(row["OneStarCount"]) : 0,
                        PercentPositive = row["PercentPositive"] != DBNull.Value ? Convert.ToDouble(row["PercentPositive"]) : 0
                    };
                }

                return new ReviewAnalytics();
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting analytics: " + ex.Message);
            }
        }

        /// <summary>
        /// Get review statistics by accommodation
        /// </summary>
        public DataTable GetReviewsByAccommodation()
        {
            try
            {
                return _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT A.AccomName,
                             COUNT(GR.ID) AS TotalReviews,
                             AVG(CAST(GR.OverallRating AS FLOAT)) AS AvgRating
                      FROM Guest_Reviews GR
                      INNER JOIN Reservation R ON R.ID = GR.Reservation_ID
                      INNER JOIN Reservation_Accommodation RA ON RA.Reservation_ID = R.ID
                      INNER JOIN Accommodation A ON A.ID = RA.Accommodation_ID
                      WHERE GR.Status = 'APPROVED'
                      GROUP BY A.AccomName
                      ORDER BY AvgRating DESC",
                    null);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting reviews by accommodation: " + ex.Message);
            }
        }

        #endregion
    }

    #region Data Transfer Objects

    /// <summary>
    /// Review submission data
    /// </summary>
    public class ReviewSubmission
    {
        public long ReservationId { get; set; }
        public string CustomerPhone { get; set; }
        public byte OverallRating { get; set; }
        public byte? CleanlinessRating { get; set; }
        public byte? ServiceRating { get; set; }
        public byte? FacilitiesRating { get; set; }
        public byte? LocationRating { get; set; }
        public byte? ValueForMoneyRating { get; set; }
        public string ReviewTitle { get; set; }
        public string ReviewText { get; set; }
        public string Pros { get; set; }
        public string Cons { get; set; }
        public bool? WouldRecommend { get; set; }
        public bool? WouldReturn { get; set; }
        public string TravelType { get; set; }
        public string Source { get; set; }
    }

    /// <summary>
    /// Review operation result
    /// </summary>
    public class ReviewResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public long ReviewId { get; set; }
    }

    /// <summary>
    /// Review analytics summary
    /// </summary>
    public class ReviewAnalytics
    {
        public int TotalReviews { get; set; }
        public int ApprovedReviews { get; set; }
        public int PendingReviews { get; set; }
        public double AvgOverallRating { get; set; }
        public double AvgCleanlinessRating { get; set; }
        public double AvgServiceRating { get; set; }
        public double AvgFacilitiesRating { get; set; }
        public double AvgLocationRating { get; set; }
        public double AvgValueForMoneyRating { get; set; }
        public int FiveStarCount { get; set; }
        public int FourStarCount { get; set; }
        public int ThreeStarCount { get; set; }
        public int TwoStarCount { get; set; }
        public int OneStarCount { get; set; }
        public double PercentPositive { get; set; }
    }

    #endregion
}
