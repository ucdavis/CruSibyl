[ "$#" -eq 1 ] || { echo "1 argument required, $# provided. Useage: sh CreateMigration <MigrationName>"; exit 1; }

export Migration__UseSql=false
dotnet ef migrations add $1 --context AppDbContextSqlite --output-dir Migrations/Sqlite --project CruSibyl.Core.csproj --startup-project ../CruSibyl.Web/CruSibyl.Web.csproj -- --provider Sqlite
export Migration__UseSql=true
dotnet ef migrations add $1 --context AppDbContextSqlServer --output-dir Migrations/SqlServer --project CruSibyl.Core.csproj --startup-project ../CruSibyl.Web/CruSibyl.Web.csproj -- --provider SqlServer
# usage from PM console in the CruSibyl.Core directory: ./CreateMigration.sh <MigrationName>

echo 'All done';