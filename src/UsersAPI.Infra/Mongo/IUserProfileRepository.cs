namespace UsersAPI.Infra.Mongo;

public interface IUserProfileRepository
{
    Task<UserProfileDocument?> GetByUserIdAsync(string userId);
    Task UpsertAsync(UserProfileDocument profile);
}
