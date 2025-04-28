namespace Htmx.Components.Input.Validation;

public interface IValidator
{
    string Id { get; }
    ValidationResult Validate(string? value);
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Fail(string message) => new() { IsValid = false, ErrorMessage = message };
}

public class ValidatorRegistry
{
    private readonly Dictionary<string, IValidator> _validators = new();

    public void Register(IValidator validator)
    {
        if (_validators.ContainsKey(validator.Id))
            throw new InvalidOperationException($"Validator with ID '{validator.Id}' already registered.");
        _validators.Add(validator.Id, validator);
    }

    public IValidator? Get(string id)
    {
        _validators.TryGetValue(id, out var validator);
        return validator;
    }
}

