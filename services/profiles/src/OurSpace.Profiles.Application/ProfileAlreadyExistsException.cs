namespace OurSpace.Profiles.Application;

public sealed class ProfileAlreadyExistsException(Exception innerException)
    : Exception("This account already has a profile.", innerException);
