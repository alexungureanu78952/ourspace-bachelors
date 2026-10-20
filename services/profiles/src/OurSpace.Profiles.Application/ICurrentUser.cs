namespace OurSpace.Profiles.Application;

public interface ICurrentUser
{
    Guid? UserId { get; }
}
