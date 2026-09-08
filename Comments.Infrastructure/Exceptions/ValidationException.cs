namespace Comments.Infrastructure.Exceptions;

public sealed class ValidationException(string message) : Exception(message);
