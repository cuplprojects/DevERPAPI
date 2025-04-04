namespace ERPAPI.Model
{
    public class QC
    {
        public int QCId { get; set; }
        public bool? Language { get; set; } 
        public bool? MaxMarks { get; set; }
        public bool? Duration { get; set; }
        public bool? Status { get; set; }
        public bool? TotalQuestions { get; set; }
        public bool? SummationofMarksEqualsTotalMarks { get; set; }
        public bool? StructureOfPaper { get; set; }
        public bool? Series { get; set; }
        public bool? A { get; set; }
        public bool? B { get; set; }
        public bool? C { get; set; }
        public bool? D { get; set; }
        public int ProjectId { get; set; }
        public int QuantitySheetId { get; set; }
    }
}
