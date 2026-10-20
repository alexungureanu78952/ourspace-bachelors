namespace OurSpace.Profiles.Domain;

public sealed class ProfileValidationException(string message) : Exception(message);
