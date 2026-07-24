using MongoDB.Driver;

namespace UsersAPI.Infra.Mongo;

public class UserProfileRepository : IUserProfileRepository
{
    private readonly IMongoCollection<UserProfileDocument> _collection;

    public UserProfileRepository(UsersMongoContext context)
    {
        _collection = context.Profiles;
    }

    public async Task<UserProfileDocument?> GetByUserIdAsync(string userId) =>
        await _collection.Find(x => x.UserId == userId).FirstOrDefaultAsync();

    public Task UpsertAsync(UserProfileDocument profile) =>
        _collection.ReplaceOneAsync(
            x => x.UserId == profile.UserId,
            profile,
            new ReplaceOptions { IsUpsert = true });
}
