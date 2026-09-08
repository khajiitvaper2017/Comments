namespace Comments.Application.Abstractions;

public interface ITextValidationService
{
    string SanitizeAndValidate(string input);
}
