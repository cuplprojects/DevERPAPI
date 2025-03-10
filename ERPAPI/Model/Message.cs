namespace ERPAPI.Model
{
    public class Message
    {
        public int MessageId { get; set; }
        public string Type { get; set; }
        public string L1Title { get; set; }
        public string L1Desc { get; set; }
        public string L2Title { get; set; }
        public string L2Desc { get; set; }
        public bool Status { get; set; }
    }
}
