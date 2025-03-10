namespace ERPAPI.Model.NonDbModels
{
    public class SetNewPassword
    {
        public string UserName { get; set; }
        public string NewPassword { get; set; }
        public bool SecurityAnswersVerified { get; set; } // Flag to indicate that the security answers have been verified
    }
}
