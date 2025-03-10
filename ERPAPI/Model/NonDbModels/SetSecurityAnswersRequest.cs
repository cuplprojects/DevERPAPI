namespace ERPAPI.Model.NonDbModels
{
    public class SetSecurityAnswersRequest
    {
        public int UserId { get; set; }  // Username to identify the user
        public int SecurityQuestion1Id { get; set; }  // First security question
        public int SecurityQuestion2Id { get; set; }  // Second security question
        public string SecurityAnswer1 { get; set; }  // Answer to the first security question
        public string SecurityAnswer2 { get; set; }  // Answer to the second security question
    }
}
