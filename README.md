# CruSibyl

MVC/HTMX dashboard and Azure Functions for application, repository, package, and WebJob inventory.

- [Azure deployment and runtime settings](infrastructure/azure/README.md)
- [Dashboard behavior](DASHBOARD.md)

User sign-in uses UC Davis Entra and the `ucdPersonIAMID` claim. Users and permissions remain IAM-based; Kerberos is absent from the application model and user-management flows.

**Before deployment:** generate the SQL Server and SQLite migrations for the model change. Migration files and snapshots are intentionally untouched.

Deployment tooling requires Node.js 22.18+; application projects target .NET 10. Run `npm ci`, `npm run deployment-settings:check`, `npm test`, and `dotnet test tests/CruSibyl.Tests/CruSibyl.Tests.csproj`. In Codex, run .NET commands outside the sandbox.
