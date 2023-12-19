dotnet ef database update --startup-project ../CruSibyl.Web/CruSibyl.Web.csproj --context AppDbContextSqlServer
# dotnet ef database update --startup-project ../CruSibyl.Web/CruSibyl.Web.csproj --context AppDbContextSqlite
# usage from PM console in the CruSibyl.Core directory: ./ExecuteMigration.sh

echo 'All done';