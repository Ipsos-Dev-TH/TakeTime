namespace TakeTime.Payment.Domain.Enums;

/// <summary>
/// Methods of payment accepted by the property.
/// </summary>
public enum PaymentMethod
{
    BankTransfer = 0,
    Cash = 1,
    CreditCard = 2,
    QRCode = 3,
    PromptPay = 4,
    LinePayment = 5
}
