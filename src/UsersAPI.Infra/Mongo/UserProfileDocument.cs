using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UsersAPI.Infra.Mongo;

public class UserProfileDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string UserId { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public List<string> FavoriteGenres { get; set; } = new();
    public Dictionary<string, string> Preferences { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
