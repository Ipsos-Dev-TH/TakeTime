// TelegramService.cs
using System;
using System.Configuration;
using System.Data;
using System.Threading.Tasks;

namespace Take_Time_BangPhra.Services
{
    public class TelegramService
    {
        private readonly string _botToken;

        public TelegramService()
        {
            _botToken = ConfigurationManager.AppSettings["TelegramTokenTakeTime"];
        }

        public TelegramService(string botToken)
        {
            _botToken = botToken;
        }

        public async Task<bool> SendMessageAsync(string chatId, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(_botToken))
                {
                    System.Diagnostics.Trace.TraceError("Telegram token is missing");
                    return false;
                }

                var bot = new TelegramBot2(_botToken);
                await bot.SendMessageAsync(chatId, message);
                System.Diagnostics.Trace.TraceInformation($"Telegram message sent to {chatId}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Telegram send error: {ex.Message}");
                return false;
            }
        }

        public async Task SendReservationNotificationAsync(string reservationId, string phoneNumber,
            DateTime checkinDate, DateTime checkoutDate, string details)
        {
            string message = $"ลูกค้าจองห้องพักใหม่หมายเลขการจอง: {reservationId}\r\n" +
                           $"หมายเลขโทรศัพท์: {phoneNumber}\r\n" +
                           $"เช็คอินวันที่: {checkinDate:dd MMMM yyyy}\r\n" +
                           $"เช็คเอ้าท์วันที่: {checkoutDate:dd MMMM yyyy}\r\n" +
                           $"{details}";

            await SendMessageAsync("-4969611371", message);
        }

        public async Task SendCancellationNotificationAsync(string reservationId, DataTable reservationDetails)
        {
            string message = "ยกเลิกการจอง:\r\n";
            for (int i = 0; i < reservationDetails.Rows.Count; i++)
            {
                DateTime checkinDate = DateTime.Parse(reservationDetails.Rows[i]["CheckinDate"].ToString());
                message += $"- หมายเลข: {reservationId}\r\n" +
                          $"ห้องพัก: {reservationDetails.Rows[i]["AccomName"]}\r\n" +
                          $"เช็คอิน: {checkinDate:dd MMMM yyyy}\r\n" +
                          $"เช็คเอาท์: {reservationDetails.Rows[i]["CheckOutDate"]}\r\n" +
                          $"จำนวนคืน: {reservationDetails.Rows[i]["StayDays"]}\r\n\r\n";
            }

            await SendMessageAsync("-4969611371", message);
        }

        public async Task SendEditNotificationAsync(string reservationId, string phoneNumber,
            DateTime checkinDate, DateTime checkoutDate, string details)
        {
            string message = $"แก้ไขการจองหมายเลข: {reservationId}\r\n" +
                           $"หมายเลขโทรศัพท์: {phoneNumber}\r\n" +
                           $"เช็คอินวันที่: {checkinDate:dd MMMM yyyy}\r\n" +
                           $"เช็คเอ้าท์วันที่: {checkoutDate:dd MMMM yyyy}\r\n" +
                           $"{details}";

            await SendMessageAsync("-4969611371", message);
        }
    }
}