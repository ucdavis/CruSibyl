using System.Security.Claims;
using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using CruSibyl.Core.Services;
using CruSibyl.Web.Middleware.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Tests;

public class IamIdentityTests
{
    private static User Person(string iam = "1000255034", string email = "person@ucdavis.edu") =>
        new() { Iam = iam, Email = email, FirstName = "Test", LastName = "Person" };
    private static Claim[] Claims(string iam = "1000255034") =>
        [new(UserService.IamIdClaimType, iam), new(ClaimTypes.NameIdentifier, "entra-subject-is-not-a-campus-username")];

    [Fact]
    public async Task ExistingIamUserRetainsIdAndPermissionsWithoutDirectoryAccess()
    {
        await using var database = await Database.Create();
        var person = Person();
        person.Permissions.Add(new Permission { Role = new Role { Name = Role.Codes.Admin } });
        database.Db.Add(person);
        await database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var identity = new StubIdentity { Failure = new HttpRequestException("Directory unavailable") };
        var service = new UserService(database.Db, new HttpContextAccessor(), identity);
        var result = await service.GetUserAsync(Claims(), true);
        Assert.Equal(person.Id, result!.Id);
        Assert.Single(result.Permissions);
        Assert.Equal(0, identity.Calls);
    }

    [Fact]
    public async Task FirstSignInUsesIamAndNeverGrantsTokenRoles()
    {
        await using var database = await Database.Create();
        var directoryUser = Person();
        directoryUser.Permissions.Add(new Permission { Role = new Role { Name = Role.Codes.System } });
        var identity = new StubIdentity { Person = directoryUser };
        var service = new UserService(database.Db, new HttpContextAccessor(), identity);
        var result = await service.GetUserAsync([.. Claims(), new Claim(ClaimTypes.Role, Role.Codes.System)]);
        Assert.Equal("1000255034", identity.LastIam);
        Assert.Empty(result!.Permissions);
        Assert.Null(database.Db.Model.FindEntityType(typeof(User))!.FindProperty("Kerberos"));
        Assert.Equal(1, await database.Db.Users.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MissingEmptyOrDuplicateIamFailsBeforeLookupOrInsert()
    {
        await using var database = await Database.Create();
        var identity = new StubIdentity { Person = Person() };
        var service = new UserService(database.Db, new HttpContextAccessor(), identity);
        Claim[][] invalid = [[], [new(UserService.IamIdClaimType, "")], [.. Claims(), new(UserService.IamIdClaimType, "1000255034")]];
        foreach (var claims in invalid)
            await Assert.ThrowsAsync<IdentityResolutionException>(() => service.GetUserAsync(claims));
        Assert.Equal(0, identity.Calls);
        Assert.Empty(database.Db.Users);
    }

    [Fact]
    public async Task MismatchedIamAndEmailCollisionCannotLinkExistingAccount()
    {
        await using var database = await Database.Create();
        var existing = Person("1000000001");
        database.Db.Add(existing);
        await database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var identity = new StubIdentity { Person = Person("1000000002") };
        var service = new UserService(database.Db, new HttpContextAccessor(), identity);
        await Assert.ThrowsAsync<IdentityResolutionException>(() => service.GetUserAsync(Claims()));
        identity.Person = Person();
        await Assert.ThrowsAsync<IdentityResolutionException>(() => service.GetUserAsync(Claims()));
        Assert.Single(database.Db.Users);
        Assert.Equal("1000000001", existing.Iam);
    }

    [Fact]
    public async Task DirectoryFailureDoesNotCreateUser()
    {
        await using var database = await Database.Create();
        var service = new UserService(database.Db, new HttpContextAccessor(), new StubIdentity { Failure = new HttpRequestException() });
        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetUserAsync(Claims()));
        Assert.Empty(database.Db.Users);
    }

    [Fact]
    public async Task AdminLookupSupportsEmailAndIamAndPreservesPermissions()
    {
        await using var database = await Database.Create();
        var identity = new StubIdentity { Person = Person() };
        var service = new UserService(database.Db, new HttpContextAccessor(), identity);
        var byEmail = await service.GetUser("person@ucdavis.edu", true);
        byEmail!.Permissions.Add(new Permission { Role = new Role { Name = Role.Codes.Admin } });
        await database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var byIam = await service.GetUser(byEmail.Iam, true);
        Assert.Equal(byEmail.Id, byIam!.Id);
        Assert.Single(byIam.Permissions);
        Assert.Equal(1, identity.Calls);
        Assert.Null(await service.GetUser("old-campus-username"));
    }

    [Fact]
    public async Task PermissionsAreReadFromDatabaseAndRevocationTakesEffect()
    {
        await using var database = await Database.Create();
        var person = Person();
        database.Db.Add(person);
        await database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([.. Claims(), new Claim(ClaimTypes.Role, Role.Codes.System)], "Entra"));
        var handler = new PermissionHandler(database.Db);
        async Task<bool> Allowed()
        {
            var requirement = PermissionRequirement.ForRoles([Role.Codes.Admin]);
            var context = new AuthorizationHandlerContext([requirement], principal, null);
            await handler.HandleAsync(context);
            return context.HasSucceeded;
        }
        Assert.False(await Allowed());
        var permission = new Permission { User = person, Role = new Role { Name = Role.Codes.Admin } };
        database.Db.Add(permission);
        await database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.True(await Allowed());
        database.Db.Remove(permission);
        await database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.False(await Allowed());
    }

    [Fact]
    public async Task DirectoryResolvesPeopleAndRejectsAmbiguousOrMismatchedResults()
    {
        var client = new StubDirectory();
        var identity = new IdentityService(client);
        var result = await identity.GetByEmail("person@ucdavis.edu");
        Assert.Equal("1000255034", result!.Iam);
        Assert.Equal("mothra", result.MothraId);
        client.SearchResults = [new("1000255034", "person@ucdavis.edu"), new("1000000001", "person@ucdavis.edu")];
        await Assert.ThrowsAsync<IdentityResolutionException>(() => identity.GetByEmail("person@ucdavis.edu"));
        client.People = [new("1000000001", "Test", "Person", "mothra")];
        await Assert.ThrowsAsync<IdentityResolutionException>(() => identity.GetByIamId("1000255034"));
        client.People = [new("1000255034", "Test", "Person", "mothra")];
        client.Contacts = [new("1000000001", "person@ucdavis.edu")];
        await Assert.ThrowsAsync<IdentityResolutionException>(() => identity.GetByIamId("1000255034"));
    }

    private sealed class StubDirectory : IIamDirectoryClient
    {
        public IReadOnlyList<IamPerson> People = [new("1000255034", "Test", "Person", "mothra")];
        public IReadOnlyList<IamContact> Contacts = [new("1000255034", "person@ucdavis.edu")];
        public IReadOnlyList<IamContact> SearchResults = [new("1000255034", "person@ucdavis.edu")];
        public Task<IReadOnlyList<IamPerson>> GetPeople(string iamId) => Task.FromResult(People);
        public Task<IReadOnlyList<IamContact>> GetContacts(string iamId) => Task.FromResult(Contacts);
        public Task<IReadOnlyList<IamContact>> SearchEmail(string email) => Task.FromResult(SearchResults);
    }

    private sealed class StubIdentity : IIdentityService
    {
        public User? Person;
        public Exception? Failure;
        public int Calls;
        public string? LastIam;
        public Task<User?> GetByEmail(string email) { Calls++; return Failure != null ? Task.FromException<User?>(Failure) : Task.FromResult(Person); }
        public Task<User?> GetByIamId(string iam) { LastIam = iam; return GetByEmail(""); }
    }

    private sealed class Database(SqliteConnection connection, AppDbContextSqlite db) : IAsyncDisposable
    {
        public AppDbContextSqlite Db => db;
        public static async Task<Database> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var db = new AppDbContextSqlite(new DbContextOptionsBuilder<AppDbContextSqlite>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return new Database(connection, db);
        }
        public async ValueTask DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
