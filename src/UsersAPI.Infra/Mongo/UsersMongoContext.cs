using MongoDB.Driver;

namespace UsersAPI.Infra.Mongo;

public class UsersMongoContext
{
    private readonly IMongoDatabase _database;

    public UsersMongoContext(string connectionString, string databaseName)
    {
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<UserProfileDocument> Profiles => _database.GetCollection<UserProfileDocument>("profiles");
}
