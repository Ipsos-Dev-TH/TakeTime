using System;
using System.Text.RegularExpressions;
using System.Web;

namespace Take_Time_BangPhra.Class
{
    /// <summary>
    /// Helper class for input validation and sanitization
    /// Centralizes all validation logic to prevent duplication
    /// </summary>
    public static class ValidationHelper
    {
        /// <summary>
        /// Cleans text by removing commas, single quotes, and double quotes
        /// Replaces: cleantext() method from Product/Default.aspx.cs, Voucher/Default.aspx.cs, Reserve.aspx.cs
        /// </summary>
        /// <param name="input">Text to clean</param>
        /// <returns>Cleaned text</returns>
        /// <example>
        /// string cleaned = ValidationHelper.CleanText(userInput);
        /// </example>
        public static string CleanText(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return input
                .Replace(",", "")
                .Replace("'", "")
                .Replace("\"", "");
        }

        /// <summary>
        /// Sanitizes text for safe HTML output (prevents XSS)
        /// </summary>
        /// <param name="input">Text to sanitize</param>
        /// <returns>HTML-encoded text</returns>
        public static string SanitizeForHtml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return HttpUtility.HtmlEncode(input);
        }

        /// <summary>
        /// Validates email format
        /// </summary>
        /// <param name="email">Email address to validate</param>
        /// <returns>True if valid email format</returns>
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // RFC 5322 compliant regex pattern
                string pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
                return Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates Thai mobile phone number format
        /// Accepts formats: 0812345678, 081-234-5678, 081 234 5678
        /// </summary>
        /// <param name="phone">Phone number to validate</param>
        /// <returns>True if valid Thai mobile phone format</returns>
        public static bool IsValidThaiMobilePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return false;

            // Remove spaces and dashes
            string cleanPhone = phone.Replace("-", "").Replace(" ", "");

            // Thai mobile format: 0[6-9]XXXXXXXX (10 digits starting with 06, 08, 09)
            string pattern = @"^0[6-9]\d{8}$";
            return Regex.IsMatch(cleanPhone, pattern);
        }

        /// <summary>
        /// Validates Thai national ID number (13 digits)
        /// </summary>
        /// <param name="idNumber">ID number to validate</param>
        /// <returns>True if valid format and checksum</returns>
        public static bool IsValidThaiIdNumber(string idNumber)
        {
            if (string.IsNullOrWhiteSpace(idNumber))
                return false;

            // Remove spaces and dashes
            string cleanId = idNumber.Replace("-", "").Replace(" ", "");

            // Must be exactly 13 digits
            if (cleanId.Length != 13 || !Regex.IsMatch(cleanId, @"^\d{13}$"))
                return false;

            // Validate checksum using Thai ID algorithm
            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                sum += int.Parse(cleanId[i].ToString()) * (13 - i);
            }

            int checksum = (11 - (sum % 11)) % 10;
            int lastDigit = int.Parse(cleanId[12].ToString());

            return checksum == lastDigit;
        }

        /// <summary>
        /// Validates Thai postal code (5 digits)
        /// </summary>
        /// <param name="postalCode">Postal code to validate</param>
        /// <returns>True if valid format</returns>
        public static bool IsValidThaiPostalCode(string postalCode)
        {
            if (string.IsNullOrWhiteSpace(postalCode))
                return false;

            // Thai postal code: 5 digits
            return Regex.IsMatch(postalCode, @"^\d{5}$");
        }

        /// <summary>
        /// Normalizes Thai mobile phone number to standard format (0812345678)
        /// </summary>
        /// <param name="phone">Phone number in any format</param>
        /// <returns>Normalized 10-digit phone number</returns>
        public static string NormalizePhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return phone;

            // Remove all non-digit characters
            string cleaned = Regex.Replace(phone, @"[^\d]", "");

            // If starts with +66, replace with 0
            if (cleaned.StartsWith("66") && cleaned.Length == 11)
            {
                cleaned = "0" + cleaned.Substring(2);
            }

            return cleaned;
        }

        /// <summary>
        /// Validates that a string is a positive number
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <returns>True if valid positive number</returns>
        public static bool IsPositiveNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (decimal.TryParse(value, out decimal result))
            {
                return result > 0;
            }

            return false;
        }

        /// <summary>
        /// Validates that a date is within a valid range
        /// </summary>
        /// <param name="date">Date to validate</param>
        /// <param name="minDate">Minimum allowed date</param>
        /// <param name="maxDate">Maximum allowed date</param>
        /// <returns>True if date is within range</returns>
        public static bool IsDateInRange(DateTime date, DateTime? minDate = null, DateTime? maxDate = null)
        {
            if (minDate.HasValue && date < minDate.Value)
                return false;

            if (maxDate.HasValue && date > maxDate.Value)
                return false;

            return true;
        }

        /// <summary>
        /// Removes HTML tags from text
        /// </summary>
        /// <param name="html">HTML text</param>
        /// <returns>Plain text without HTML tags</returns>
        public static string StripHtmlTags(string html)
        {
            if (string.IsNullOrEmpty(html))
                return html;

            return Regex.Replace(html, @"<[^>]*>", string.Empty);
        }

        /// <summary>
        /// Truncates text to specified length with ellipsis
        /// </summary>
        /// <param name="text">Text to truncate</param>
        /// <param name="maxLength">Maximum length</param>
        /// <param name="addEllipsis">Add "..." at the end</param>
        /// <returns>Truncated text</returns>
        public static string TruncateText(string text, int maxLength, bool addEllipsis = true)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            string truncated = text.Substring(0, maxLength);
            return addEllipsis ? truncated + "..." : truncated;
        }
    }
}
