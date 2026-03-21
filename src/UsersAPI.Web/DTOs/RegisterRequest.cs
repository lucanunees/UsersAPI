namespace UsersAPI.Web
{
    public record RegisterRequest(
      string Email,
      string Password,
      string FullName,
      string Role = "User"
  );
}
