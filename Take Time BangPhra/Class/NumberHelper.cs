using System;
using System.Net;
using System.Net.Mail;

namespace Take_Time_BangPhra.Class
{
    /// <summary>
    /// Helper class for number formatting and calculations
    /// Replaces utility methods from deprecated Reservation class
    /// </summary>
    public class NumberHelper
    {
        /// <summary>
        /// Rounds a number to 2 decimal places
        /// </summary>
        /// <param name="num">Number to round</param>
        /// <returns>Number rounded to 2 decimal places</returns>
        public static double TwoDecimalPoints(double num)
        {
            var totalCost = Convert.ToDouble(String.Format("{0:0.00}", num));
            return totalCost;
        }

        /// <summary>
        /// Rounds a decimal to 2 decimal places
        /// </summary>
        /// <param name="num">Decimal to round</param>
        /// <returns>Decimal rounded to 2 decimal places</returns>
        public static decimal TwoDecimalPoints(decimal num)
        {
            return Math.Round(num, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Legacy email sending method from deprecated Reservation class
        /// Sends email with attachments using SMTP
        /// </summary>
        public static void SendEmail(string SMTP, int Port, bool EnableSsl, bool UseDefaultCredentials, string from, string password, string to, string cc, string subject, string body, Attachment[] data)
        {
            MailMessage mail = new MailMessage(from, to);
            SmtpClient client = new SmtpClient();
            client.Host = SMTP;
            client.Port = Port;
            client.EnableSsl = EnableSsl;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = UseDefaultCredentials;
            client.Credentials = new NetworkCredential(from, password);
            try
            {
                mail.CC.Add(cc);
            }
            catch { }
            mail.Subject = subject;
            mail.Body = body;
            mail.IsBodyHtml = true;
            try
            {
                for (int i = 0; i < data.Length; i++)
                {
                    mail.Attachments.Add(data[i]);
                }

            }
            catch { }
            client.Send(mail);
        }
    }
}
