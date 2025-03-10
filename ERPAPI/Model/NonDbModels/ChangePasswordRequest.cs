namespace ERPAPI.Model.NonDbModels
{
    public class ChangePasswordRequest
    {
        public string OldPassword { get; set; }  // Add UserName for identification
        public string NewPassword { get; set; }
    }

}
