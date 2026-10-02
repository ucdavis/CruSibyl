using CruSibyl.Core.Domain;
using CruSibyl.Core.Models.Settings;
using Ietws;
using Microsoft.Extensions.Options;

namespace CruSibyl.Core.Services;

public interface IIdentityService
{
    Task<User?> GetByEmail(string email);
    Task<User?> GetByIamId(string iamId);
}

public record IamPerson(string IamId, string FirstName, string LastName, string MothraId);
public record IamContact(string IamId, string Email);

public interface IIamDirectoryClient
{
    Task<IReadOnlyList<IamPerson>> GetPeople(string iamId);
    Task<IReadOnlyList<IamContact>> GetContacts(string iamId);
    Task<IReadOnlyList<IamContact>> SearchEmail(string email);
}

public class IamDirectoryClient(IOptions<AuthSettings> settings) : IIamDirectoryClient
{
    public async Task<IReadOnlyList<IamPerson>> GetPeople(string iamId)
    {
        var result = await new IetClient(settings.Value.IamKey).People.Get(iamId);
        return result.ResponseData.Results.Select(p => new IamPerson(p.IamId, p.FirstName, p.LastName, p.MothraId)).ToArray();
    }

    public async Task<IReadOnlyList<IamContact>> GetContacts(string iamId)
    {
        var result = await new IetClient(settings.Value.IamKey).Contacts.Get(iamId);
        return result.ResponseData.Results.Select(p => new IamContact(p.IamId, p.Email)).ToArray();
    }

    public async Task<IReadOnlyList<IamContact>> SearchEmail(string email)
    {
        var result = await new IetClient(settings.Value.IamKey).Contacts.Search(ContactSearchField.email, email);
        return result.ResponseData.Results.Select(p => new IamContact(p.IamId, p.Email)).ToArray();
    }
}

public class IdentityResolutionException(string message) : Exception(message);

public class IdentityService(IIamDirectoryClient directory) : IIdentityService
{
    public async Task<User?> GetByEmail(string email)
    {
        var matches = await directory.SearchEmail(email.Trim());
        var ids = matches.Where(p => string.Equals(p.Email, email.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(p => p.IamId).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) return null;
        if (ids.Length != 1 || string.IsNullOrWhiteSpace(ids[0]))
            throw new IdentityResolutionException("Email does not resolve to one IAM identity.");
        return await GetByIamId(ids[0]);
    }

    public async Task<User?> GetByIamId(string iamId)
    {
        var people = await directory.GetPeople(iamId);
        if (people.Count == 0) return null;
        if (people.Count != 1 || people[0].IamId != iamId)
            throw new IdentityResolutionException("Directory person does not match the requested IAM identity.");
        var contacts = await directory.GetContacts(iamId);
        if (contacts.Any(c => c.IamId != iamId))
            throw new IdentityResolutionException("Directory contacts do not match the requested IAM identity.");
        var emails = contacts.Select(c => c.Email).Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (emails.Length != 1)
            throw new IdentityResolutionException("Directory must provide one unambiguous email address.");
        var person = people[0];
        return new User { Iam = iamId, FirstName = person.FirstName, LastName = person.LastName,
            Email = emails[0], MothraId = person.MothraId ?? "" };
    }
}
