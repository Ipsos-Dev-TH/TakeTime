namespace Take_Time_BangPhra.PolicyPages
{
    /// <summary>หน้าสาธารณะ ข้อกำหนดและเงื่อนไข (Terms & Conditions) — /Policy/Terms</summary>
    public partial class Terms : PolicyPage
    {
        public override string PolicyKey
        {
            get { return BookingPolicy.KeyTerms; }
        }
    }
}
