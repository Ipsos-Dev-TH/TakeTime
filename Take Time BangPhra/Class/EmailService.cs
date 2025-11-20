// EmailService.cs
using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;

namespace Take_Time_BangPhra.Services
{
    public class EmailService
    {
        private readonly string _smtpServer;
        private readonly int _port;
        private readonly bool _enableSsl;
        private readonly bool _useDefaultCredentials;
        private readonly string _fromEmail;
        private readonly string _password;
        private readonly string _ccEmail;

        public EmailService()
        {
            _smtpServer = ConfigurationManager.AppSettings["SMTP"];
            _port = Convert.ToInt32(ConfigurationManager.AppSettings["SMTP_Port"]);
            _enableSsl = Convert.ToBoolean(ConfigurationManager.AppSettings["SMTP_EnableSsl"]);
            _useDefaultCredentials = Convert.ToBoolean(ConfigurationManager.AppSettings["SMTP_UseDefaultCredentials"]);
            _fromEmail = ConfigurationManager.AppSettings["Email_From"];
            _password = ConfigurationManager.AppSettings["Email_Password_From"];
            _ccEmail = ConfigurationManager.AppSettings["Email_CC"];
        }

        public void SendEmail(string to, string subject, string body, Attachment[] attachments = null)
        {
            using (MailMessage mail = new MailMessage(_fromEmail, to))
            using (SmtpClient client = new SmtpClient(_smtpServer, _port))
            {
                client.EnableSsl = _enableSsl;
                client.UseDefaultCredentials = _useDefaultCredentials;
                client.Credentials = new NetworkCredential(_fromEmail, _password);

                if (!string.IsNullOrEmpty(_ccEmail))
                {
                    mail.CC.Add(_ccEmail);
                }

                mail.Subject = subject;
                mail.Body = body;
                mail.IsBodyHtml = true;

                if (attachments != null)
                {
                    foreach (var attachment in attachments)
                    {
                        mail.Attachments.Add(attachment);
                    }
                }

                client.Send(mail);
            }
        }

        public void SendReceiptEmail(string toEmail, string receiptId, DateTime docDate, string pdfFilePath)
        {
            string docCreateThaiDate = docDate.ToString("ddMM") + (docDate.Year + 543).ToString();
            string subject = $"[{docCreateThaiDate}][INV][{receiptId}]";
            string body = @"เรียน ลูกค้าผู้มีอุปการะคุณ <br /><br />
                          หจก.แอม แฮปปี้เนส (Take Time) ได้แนบใบกำกับภาษี/ใบเสร็จรับเงินมาพร้อมกับอีเมล์ฉบับนี้ 
                          ท่านสามารถเปิดดูได้โดยคลิกไฟล์แนบ (PDF File)<br />
                          ขอแสดงความนับถือ<br />
                          หจก.แอม แฮปปี้เนส (Take Time)";

            byte[] bytes = System.IO.File.ReadAllBytes(pdfFilePath);
            var memoryStream = new System.IO.MemoryStream(bytes);
            var attachment = new Attachment(memoryStream, $"{receiptId}_etax.pdf");

            SendEmail(toEmail, subject, body, new Attachment[] { attachment });
        }
    }
}